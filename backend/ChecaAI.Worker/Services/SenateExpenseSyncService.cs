using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Worker.Services;

/// <summary>
/// Syncs parliamentary expenses for all active Senators.
/// Source: Senado Federal open data API — /senador/{codigo}/transparencia/despesas
/// Runs once at startup (after a short delay), then every 12 hours.
/// </summary>
public class SenateExpenseSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SenateExpenseSyncService> _logger;

    private const string SenadoBaseUrl = "https://legis.senado.leg.br/dadosabertos";
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(12);
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(300);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public SenateExpenseSyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<SenateExpenseSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SenateExpenseSync] Service started — first run in {Delay}min", StartupDelay.TotalMinutes);

        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncExpensesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SenateExpenseSync] Unexpected error during sync");
            }

            await Task.Delay(SyncInterval, stoppingToken);
        }
    }

    private async Task SyncExpensesAsync(CancellationToken ct)
    {
        var years = new[] { DateTime.UtcNow.Year, DateTime.UtcNow.Year - 1 };
        var client = _httpClientFactory.CreateClient("StateDeputy");

        // Load all active senators from DB
        List<(int Id, string ExternalId, string Name)> senators;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            senators = await db.Politicians
                .Where(p => p.PoliticalPosition == "Senator" && p.IsActive && p.ExternalId != null)
                .Select(p => new { p.Id, p.ExternalId, p.FullName })
                .AsNoTracking()
                .ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(x => (x.Id, x.ExternalId!, x.FullName)).ToList(), ct);
        }

        _logger.LogInformation("[SenateExpenseSync] Starting expense sync for {Count} senators, years: {Years}",
            senators.Count, string.Join(", ", years));

        var totalAdded = 0;

        foreach (var (id, externalId, name) in senators)
        {
            if (ct.IsCancellationRequested) break;

            foreach (var year in years)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var expenses = await FetchExpensesAsync(client, externalId, year, ct);
                    if (expenses.Count > 0)
                    {
                        var added = await PersistExpensesAsync(id, year, expenses, ct);
                        totalAdded += added;
                        if (added > 0)
                            _logger.LogDebug("[SenateExpenseSync] Senator {Name} ({Year}): +{Added} expenses", name, year, added);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[SenateExpenseSync] Failed to sync senator {Name} year {Year} — skipping", name, year);
                }

                await Task.Delay(RateLimitDelay, ct);
            }
        }

        _logger.LogInformation("[SenateExpenseSync] Done — added {Total} new expense records", totalAdded);
    }

    private async Task<List<SenadoDespesa>> FetchExpensesAsync(
        HttpClient client, string codigoParlamentar, int year, CancellationToken ct)
    {
        var url = $"{SenadoBaseUrl}/senador/{codigoParlamentar}/transparencia/despesas?ano={year}";

        // Retries transient failures (network errors, 5xx, 429) so a single hiccup doesn't get
        // treated the same as "senator has no expenses" and silently leave zero data.
        const int maxAttempts = 3;
        string? json = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await client.GetAsync(url, ct);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return [];

                if (response.IsSuccessStatusCode)
                {
                    json = await response.Content.ReadAsStringAsync(ct);
                    break;
                }

                if (attempt == maxAttempts)
                {
                    _logger.LogWarning("[SenateExpenseSync] Giving up after {Attempts} attempts, HTTP {Status} for senator {Id} year {Year}",
                        maxAttempts, response.StatusCode, codigoParlamentar, year);
                    return [];
                }

                _logger.LogDebug("[SenateExpenseSync] HTTP {Status} on attempt {Attempt}/{Max} for senator {Id}, retrying",
                    response.StatusCode, attempt, maxAttempts, codigoParlamentar);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogDebug(ex, "[SenateExpenseSync] HTTP error on attempt {Attempt}/{Max} for senator {Id}, retrying", attempt, maxAttempts, codigoParlamentar);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[SenateExpenseSync] Giving up after {Attempts} attempts for senator {Id}", maxAttempts, codigoParlamentar);
                return [];
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
        }

        if (json == null) return [];

        try
        {
            return ParseSenadoExpenses(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SenateExpenseSync] Failed to parse response for senator {Id} year {Year}", codigoParlamentar, year);
            return [];
        }
    }

    private static List<SenadoDespesa> ParseSenadoExpenses(string json)
    {
        var result = new List<SenadoDespesa>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Navigate: DespesasParlamentar.Parlamentar.Despesas.Periodo[]
        if (!root.TryGetProperty("DespesasParlamentar", out var despesasParlamentar)) return result;
        if (!despesasParlamentar.TryGetProperty("Parlamentar", out var parlamentar)) return result;
        if (!parlamentar.TryGetProperty("Despesas", out var despesasRoot)) return result;
        if (!despesasRoot.TryGetProperty("Periodo", out var periodoEl)) return result;

        // Periodo can be a single object or array
        if (periodoEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var periodo in periodoEl.EnumerateArray())
                ExtractDespesasFromPeriodo(periodo, result);
        }
        else if (periodoEl.ValueKind == JsonValueKind.Object)
        {
            ExtractDespesasFromPeriodo(periodoEl, result);
        }

        return result;
    }

    private static void ExtractDespesasFromPeriodo(JsonElement periodo, List<SenadoDespesa> result)
    {
        if (!periodo.TryGetProperty("Despesas", out var despesasEl)) return;
        if (!despesasEl.TryGetProperty("Despesa", out var despesaEl)) return;

        var mes = periodo.TryGetProperty("Mes", out var mesEl) ? mesEl.GetString() : null;
        var ano = periodo.TryGetProperty("Ano", out var anoEl) ? anoEl.GetString() : null;

        // Despesa can be a single object or array
        if (despesaEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var d in despesaEl.EnumerateArray())
                result.Add(ParseDespesa(d, mes, ano));
        }
        else if (despesaEl.ValueKind == JsonValueKind.Object)
        {
            result.Add(ParseDespesa(despesaEl, mes, ano));
        }
    }

    private static SenadoDespesa ParseDespesa(JsonElement el, string? mes, string? ano)
    {
        static string? Str(JsonElement e, string prop) =>
            e.TryGetProperty(prop, out var v) ? v.GetString() : null;

        static decimal Dec(JsonElement e, string prop)
        {
            if (!e.TryGetProperty(prop, out var v)) return 0m;
            if (v.ValueKind == JsonValueKind.Number) return v.GetDecimal();
            return decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;
        }

        return new SenadoDespesa
        {
            TipoDespesa = Str(el, "TipoDespesa") ?? "Despesa Parlamentar",
            DataDespesa = Str(el, "DataDespesa"),
            ValorReembolsado = Dec(el, "ValorReembolsado"),
            NumDocumento = Str(el, "NumDocumento"),
            Beneficiario = Str(el, "Beneficiario") ?? Str(el, "NomeBeneficiario"),
            Mes = mes,
            Ano = ano
        };
    }

    private async Task<int> PersistExpensesAsync(
        int politicianId, int year, List<SenadoDespesa> expenses, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        // Load existing document numbers for this politician + year
        var existingDocNumbers = (await db.PoliticianExpenses
            .Where(e => e.PoliticianId == politicianId && e.Year == year)
            .Select(e => e.DocumentNumber)
            .ToListAsync(ct))
            .Where(d => d != null)
            .Select(d => d!)
            .ToHashSet();

        var added = 0;

        foreach (var expense in expenses)
        {
            // Use NumDocumento as dedup key; skip if empty and amount is 0
            if (expense.ValorReembolsado <= 0 && string.IsNullOrWhiteSpace(expense.NumDocumento))
                continue;

            var docKey = expense.NumDocumento ?? $"senado-{year}-{expense.TipoDespesa}-{expense.ValorReembolsado:F2}";

            if (existingDocNumbers.Contains(docKey))
                continue;

            if (!DateTime.TryParse(expense.DataDespesa, out var expDate))
            {
                if (int.TryParse(expense.Mes, out var m) && m >= 1 && m <= 12)
                    expDate = new DateTime(year, m, 1, 0, 0, 0, DateTimeKind.Utc);
                else
                    expDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
            else
            {
                expDate = DateTime.SpecifyKind(expDate, DateTimeKind.Utc);
            }

            var description = string.IsNullOrWhiteSpace(expense.Beneficiario)
                ? expense.TipoDespesa
                : $"{expense.TipoDespesa} — {expense.Beneficiario}";

            db.PoliticianExpenses.Add(new PoliticianExpense
            {
                PoliticianId = politicianId,
                Description = description.Length > 200 ? description[..200] : description,
                Category = expense.TipoDespesa.Length > 100 ? expense.TipoDespesa[..100] : expense.TipoDespesa,
                Amount = expense.ValorReembolsado,
                Provider = expense.Beneficiario?.Length > 100 ? expense.Beneficiario[..100] : expense.Beneficiario,
                DocumentNumber = docKey.Length > 50 ? docKey[..50] : docKey,
                ExpenseDate = expDate,
                Month = expense.Mes ?? expDate.Month.ToString(),
                Year = year,
                ExternalId = docKey.Length > 20 ? docKey[..20] : docKey,
                CreatedAt = DateTime.UtcNow
            });

            existingDocNumbers.Add(docKey);
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        return added;
    }

    // ---- Internal DTOs ----

    private sealed class SenadoDespesa
    {
        public string TipoDespesa { get; set; } = string.Empty;
        public string? DataDespesa { get; set; }
        public decimal ValorReembolsado { get; set; }
        public string? NumDocumento { get; set; }
        public string? Beneficiario { get; set; }
        public string? Mes { get; set; }
        public string? Ano { get; set; }
    }
}
