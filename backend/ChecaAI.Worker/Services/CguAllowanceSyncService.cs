using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Worker.Services;

/// <summary>
/// Syncs parliamentary allowances (auxílios) from the Portal da Transparência CGU API.
///
/// Endpoint used:
///   GET https://portaldatransparencia.gov.br/api-de-dados/servidores/indenizacoes
///     ?cpf={cpf}&mesAno={AAAAMM}&pagina=1&tamanhoPagina=10
///   Header: chave-api-dados: {CguApiKey}
///
/// Types of allowances extracted per response row:
///   - Auxílio Alimentação
///   - Auxílio Transporte
///   - Auxílio Pré-Escolar
///   - Auxílio Saúde
///   - Auxílio Natalidade
///   - Auxílio Moradia
///   - Auxílio Escolar
///   - Abono de Residência
///   - Indenização de Férias
///
/// Runs every 24 hours. Covers the last 12 months for each politician with CPF.
/// Upserts by (PoliticianId, Year, Month, AllowanceType) to avoid duplicates.
/// </summary>
public class CguAllowanceSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CguAllowanceSyncService> _logger;
    private readonly bool _apiKeyConfigured;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private const string IndenizacoesPath = "/api-de-dados/servidores/indenizacoes";

    // Maps JSON property names → AllowanceType display name
    private static readonly Dictionary<string, string> AllowanceFields = new()
    {
        ["auxilioAlimentacao"]  = "Auxílio Alimentação",
        ["auxilioTransporte"]   = "Auxílio Transporte",
        ["auxilioPreEscolar"]   = "Auxílio Pré-Escolar",
        ["auxilioSaude"]        = "Auxílio Saúde",
        ["auxilioNatalidade"]   = "Auxílio Natalidade",
        ["auxilioMoradia"]      = "Auxílio Moradia",
        ["auxilioEscolar"]      = "Auxílio Escolar",
        ["abonoResidencia"]     = "Abono de Residência",
        ["indenizacaoFerias"]   = "Indenização de Férias",
    };

    public CguAllowanceSyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CguAllowanceSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKeyConfigured = !string.IsNullOrWhiteSpace(configuration["CguApiKey"]);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_apiKeyConfigured)
        {
            _logger.LogWarning("[CguAllowanceSync] CguApiKey not configured — service will not run. " +
                               "Register at https://portaldatransparencia.gov.br/api-de-dados and add to appsettings.json.");
            return;
        }

        // Stagger startup — give salary sync a head start
        await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);
        if (stoppingToken.IsCancellationRequested) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[CguAllowanceSync] Starting allowance sync cycle...");

            try
            {
                await SyncAllowancesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[CguAllowanceSync] Unhandled error during sync cycle");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task SyncAllowancesAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Cgu");

        // Only senators and federal deputies (with CPF, already in CGU system)
        List<(int Id, string Cpf, string FullName)> politicians;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            politicians = await db.Politicians
                .Where(p => p.IsActive
                         && p.Cpf != null
                         && (p.PoliticalPosition == "Senator" || p.PoliticalPosition == "Federal Deputy"))
                .Select(p => new { p.Id, p.Cpf, p.FullName })
                .AsNoTracking()
                .ToListAsync(ct)
                .ContinueWith(t => t.Result
                    .Select(x => (x.Id, x.Cpf!, x.FullName))
                    .ToList(), ct);
        }

        _logger.LogInformation("[CguAllowanceSync] Fetching allowances for {Count} politicians...", politicians.Count);

        var now = DateTime.UtcNow;
        var months = Enumerable.Range(0, 12)
            .Select(i => now.AddMonths(-i))
            .Select(d => (d.Year, d.Month))
            .ToList();

        var totalAdded = 0;

        foreach (var (politicianId, cpf, fullName) in politicians)
        {
            if (ct.IsCancellationRequested) break;

            foreach (var (year, month) in months)
            {
                var added = await SyncMonthAsync(client, politicianId, cpf, year, month, ct);
                totalAdded += added;
            }

            await Task.Delay(100, ct); // CGU rate limit
        }

        _logger.LogInformation("[CguAllowanceSync] Sync complete — {Added} new allowance records", totalAdded);
    }

    private async Task<int> SyncMonthAsync(
        HttpClient client, int politicianId, string cpf, int year, int month, CancellationToken ct)
    {
        // Check if already synced for this politician × year × month
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            var already = await db.Allowances.AnyAsync(
                a => a.PoliticianId == politicianId && a.Year == year && a.Month == month, ct);
            if (already) return 0;
        }

        var mesAno = $"{year:D4}{month:D2}";
        var url = $"{IndenizacoesPath}?cpf={Uri.EscapeDataString(cpf)}&mesAno={mesAno}&pagina=1&tamanhoPagina=10";

        // Retries 429/5xx/network hiccups instead of permanently giving up on this CPF+month.
        const int maxAttempts = 3;
        string? json = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var resp = await client.GetAsync(url, ct);

                if (resp.IsSuccessStatusCode)
                {
                    json = await resp.Content.ReadAsStringAsync(ct);
                    break;
                }

                if (attempt == maxAttempts)
                {
                    _logger.LogDebug("[CguAllowanceSync] {Cpf} {MesAno}: giving up after {Attempts} attempts, HTTP {Status}",
                        MaskCpf(cpf), mesAno, maxAttempts, (int)resp.StatusCode);
                    return 0;
                }
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogDebug(ex, "[CguAllowanceSync] {Cpf} {MesAno}: attempt {Attempt}/{Max} failed, retrying", MaskCpf(cpf), mesAno, attempt, maxAttempts);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[CguAllowanceSync] {Cpf} {MesAno}: giving up after {Attempts} attempts", MaskCpf(cpf), mesAno, maxAttempts);
                return 0;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }
            catch (OperationCanceledException)
            {
                // Must not escape uncaught — the outer ExecuteAsync loop treats any
                // OperationCanceledException seen while stoppingToken.IsCancellationRequested
                // as a deliberate shutdown and permanently stops this service's periodic sync.
                return 0;
            }
        }

        if (json == null) return 0;

        // Detect AWS WAF CAPTCHA (200 OK with HTML body)
        if (json.TrimStart().StartsWith('<'))
        {
            _logger.LogWarning("[CguAllowanceSync] CGU API returned WAF/CAPTCHA HTML — allowance data unavailable from this environment.");
            return 0;
        }

        var root = JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);

        // Response is an array; use first element (or the whole array)
        JsonElement[] rows;
        if (root.ValueKind == JsonValueKind.Array)
        {
            rows = root.EnumerateArray().ToArray();
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            rows = [root];
        }
        else
        {
            return 0;
        }

        if (rows.Length == 0) return 0;

        var toAdd = new List<Allowance>();

        foreach (var row in rows)
        {
            foreach (var (field, displayName) in AllowanceFields)
            {
                if (!row.TryGetProperty(field, out var val) &&
                    // try camelCase variants
                    !TryGetPropertyCaseInsensitive(row, field, out val)) continue;

                var amount = val.ValueKind switch
                {
                    JsonValueKind.Number => val.GetDecimal(),
                    JsonValueKind.String => decimal.TryParse(
                        val.GetString()?.Replace(",", "."),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m,
                    _ => 0m
                };

                if (amount <= 0m) continue;

                toAdd.Add(new Allowance
                {
                    PoliticianId = politicianId,
                    AllowanceType = displayName,
                    Amount = amount,
                    Month = month,
                    Year = year,
                    Source = "CGU",
                    ExternalId = $"{cpf}-{mesAno}-{field}".Length > 100
                        ? null
                        : $"{cpf}-{mesAno}-{field}",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        if (toAdd.Count == 0) return 0;

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

            // Load existing types for this month to avoid unique-constraint violations
            var existingTypes = await db.Allowances
                .Where(a => a.PoliticianId == politicianId && a.Year == year && a.Month == month)
                .Select(a => a.AllowanceType)
                .ToListAsync(ct);
            var existingSet = new HashSet<string>(existingTypes, StringComparer.OrdinalIgnoreCase);

            var newAllowances = toAdd.Where(a => !existingSet.Contains(a.AllowanceType)).ToList();
            if (newAllowances.Count == 0) return 0;

            db.Allowances.AddRange(newAllowances);
            await db.SaveChangesAsync(ct);
            return newAllowances.Count;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string key, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string MaskCpf(string cpf) =>
        cpf.Length >= 11 ? $"***{cpf[3..6]}***" : "***";
}
