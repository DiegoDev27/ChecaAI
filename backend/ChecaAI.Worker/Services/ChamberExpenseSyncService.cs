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
/// Syncs parliamentary quota expenses (CEAP) for all active Federal Deputies.
/// Source: Câmara dos Deputados API — /deputados/{id}/despesas
/// Runs once at startup (after a short delay), then every 12 hours.
/// </summary>
public class ChamberExpenseSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChamberExpenseSyncService> _logger;

    private const string CamaraBaseUrl = "https://dadosabertos.camara.leg.br/api/v2";
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(12);
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(200);

    public ChamberExpenseSyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<ChamberExpenseSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[ChamberExpenseSync] Service started — first run in {Delay}min", StartupDelay.TotalMinutes);

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
                _logger.LogError(ex, "[ChamberExpenseSync] Unexpected error during sync");
            }

            await Task.Delay(SyncInterval, stoppingToken);
        }
    }

    private async Task SyncExpensesAsync(CancellationToken ct)
    {
        var years = new[] { DateTime.UtcNow.Year, DateTime.UtcNow.Year - 1 };
        var client = _httpClientFactory.CreateClient("StateDeputy");

        // Load all active federal deputies from DB
        List<(int Id, string ExternalId, string Name)> deputies;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            deputies = await db.Politicians
                .Where(p => p.PoliticalPosition == "Federal Deputy" && p.IsActive && p.ExternalId != null)
                .Select(p => new { p.Id, p.ExternalId, p.FullName })
                .AsNoTracking()
                .ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(x => (x.Id, x.ExternalId!, x.FullName)).ToList(), ct);
        }

        _logger.LogInformation("[ChamberExpenseSync] Starting expense sync for {Count} deputies, years: {Years}",
            deputies.Count, string.Join(", ", years));

        var totalAdded = 0;

        foreach (var (id, externalId, name) in deputies)
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
                            _logger.LogDebug("[ChamberExpenseSync] Deputy {Name} ({Year}): +{Added} expenses", name, year, added);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ChamberExpenseSync] Failed to sync deputy {Name} year {Year} — skipping", name, year);
                }

                await Task.Delay(RateLimitDelay, ct);
            }
        }

        _logger.LogInformation("[ChamberExpenseSync] Done — added {Total} new expense records", totalAdded);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private async Task<List<ExpenseRecord>> FetchExpensesAsync(
        HttpClient client, string deputyExternalId, int year, CancellationToken ct)
    {
        var all = new List<ExpenseRecord>();
        var page = 1;

        while (true)
        {
            var url = $"{CamaraBaseUrl}/deputados/{deputyExternalId}/despesas" +
                      $"?ano={year}&pagina={page}&itens=100&ordem=ASC&ordenarPor=mes";

            var result = await FetchPageWithRetryAsync(client, url, ct);
            if (result?.Dados == null || result.Dados.Count == 0)
                break;

            all.AddRange(result.Dados);

            var hasNext = result.Links?.Any(l => string.Equals(l.Rel, "next", StringComparison.OrdinalIgnoreCase)) ?? false;
            if (!hasNext) break;
            page++;
        }

        return all;
    }

    // Retries transient failures (network errors, 5xx, 429) so a single hiccup doesn't get
    // misread as "no more expenses" and silently leave a deputy with zero/partial data.
    private async Task<CamaraPagedResponse<ExpenseRecord>?> FetchPageWithRetryAsync(
        HttpClient client, string url, CancellationToken ct)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await client.GetAsync(url, ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    return JsonSerializer.Deserialize<CamaraPagedResponse<ExpenseRecord>>(json, JsonOpts);
                }

                if (attempt == maxAttempts)
                {
                    _logger.LogWarning("[ChamberExpenseSync] Giving up after {Attempts} attempts, HTTP {Status}: {Url}",
                        maxAttempts, response.StatusCode, url);
                    return null;
                }

                _logger.LogWarning("[ChamberExpenseSync] HTTP {Status} on attempt {Attempt}/{Max}, retrying: {Url}",
                    response.StatusCode, attempt, maxAttempts, url);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "[ChamberExpenseSync] Fetch failed on attempt {Attempt}/{Max}, retrying: {Url}",
                    attempt, maxAttempts, url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ChamberExpenseSync] Giving up after {Attempts} attempts: {Url}", maxAttempts, url);
                return null;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }
            catch (OperationCanceledException)
            {
                // A cancelled backoff delay must not escape as an unhandled exception — the
                // outer ExecuteAsync loop treats "OperationCanceledException while
                // stoppingToken.IsCancellationRequested" as a deliberate shutdown and breaks
                // out permanently, killing this BackgroundService's periodic sync for good.
                return null;
            }
        }

        return null;
    }

    private async Task<int> PersistExpensesAsync(
        int politicianId, int year, List<ExpenseRecord> expenses, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        // Load existing ExternalIds for this politician + year to avoid duplicates
        var existingIds = (await db.PoliticianExpenses
            .Where(e => e.PoliticianId == politicianId && e.Year == year)
            .Select(e => e.ExternalId)
            .ToListAsync(ct))
            .Where(id => id != null)
            .Select(id => id!)
            .ToHashSet();

        var added = 0;

        foreach (var expense in expenses)
        {
            var externalId = expense.CodDocumento;

            if (existingIds.Contains(externalId))
                continue;

            var description = string.IsNullOrWhiteSpace(expense.NomeFornecedor)
                ? expense.TipoDespesa
                : $"{expense.TipoDespesa} — {expense.NomeFornecedor}";

            db.PoliticianExpenses.Add(new PoliticianExpense
            {
                PoliticianId = politicianId,
                Description = description.Length > 200 ? description[..200] : description,
                Category = expense.TipoDespesa.Length > 100 ? expense.TipoDespesa[..100] : expense.TipoDespesa,
                Amount = expense.ValorDocumento,
                Provider = expense.NomeFornecedor?.Length > 100 ? expense.NomeFornecedor[..100] : expense.NomeFornecedor,
                DocumentNumber = expense.NumDocumento?.Length > 50 ? expense.NumDocumento[..50] : expense.NumDocumento,
                ExpenseDate = expense.DataDocumento == default
                    ? new DateTime(year, expense.Mes, 1, 0, 0, 0, DateTimeKind.Utc)
                    : DateTime.SpecifyKind(expense.DataDocumento, DateTimeKind.Utc),
                Month = expense.Mes.ToString(),
                Year = year,
                ExternalId = externalId.Length > 20 ? externalId[..20] : externalId,
                CreatedAt = DateTime.UtcNow
            });

            existingIds.Add(externalId);
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        return added;
    }

    // ---- DTOs ----

    private sealed class CamaraPagedResponse<T>
    {
        [JsonPropertyName("dados")]
        public List<T> Dados { get; set; } = new();
        [JsonPropertyName("links")]
        public List<CamaraLink>? Links { get; set; }
    }

    private sealed class CamaraLink
    {
        [JsonPropertyName("rel")]
        public string Rel { get; set; } = string.Empty;
    }

    private sealed class ExpenseRecord
    {
        [JsonPropertyName("ano")]
        public int Ano { get; set; }

        [JsonPropertyName("mes")]
        public int Mes { get; set; }

        [JsonPropertyName("tipoDespesa")]
        public string TipoDespesa { get; set; } = string.Empty;

        [JsonPropertyName("codDocumento")]
        public string CodDocumento { get; set; } = string.Empty;

        [JsonPropertyName("dataDocumento")]
        public DateTime DataDocumento { get; set; }

        [JsonPropertyName("numDocumento")]
        public string? NumDocumento { get; set; }

        [JsonPropertyName("valorDocumento")]
        public decimal ValorDocumento { get; set; }

        [JsonPropertyName("nomeFornecedor")]
        public string? NomeFornecedor { get; set; }

        [JsonPropertyName("cnpjCpfFornecedor")]
        public string? CnpjCpfFornecedor { get; set; }
    }
}
