using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Worker.Services;

/// <summary>
/// One-shot service that backfills CPF on Federal Deputies and Senators using
/// the official government APIs — much more reliable than name-matching TSE CSVs.
///
/// Câmara: GET /api/v2/deputados/{externalId} → cpf field
/// Senado: GET /dadosabertos/senador/{codigo} → DadosBasicosParlamentar/cpf or IdentificacaoParlamentar/CpfParlamentar
///
/// Runs once at startup (5min delay). Idempotent — skips politicians already with CPF.
/// After populating CPFs, CguSalarySyncService and CguAllowanceSyncService will
/// automatically start syncing those politicians.
/// </summary>
public class CpfBackfillService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CpfBackfillService> _logger;

    private const string CamaraBase = "https://dadosabertos.camara.leg.br/api/v2";
    private const string SenadoBase = "https://legis.senado.leg.br/dadosabertos";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public CpfBackfillService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<CpfBackfillService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 5 min — let the main sync services start first
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        if (stoppingToken.IsCancellationRequested) return;

        _logger.LogInformation("[CpfBackfill] Starting CPF backfill for deputies and senators...");

        var deputyCount  = await BackfillDeputiesAsync(stoppingToken);
        var senatorCount = await BackfillSenatorsAsync(stoppingToken);

        _logger.LogInformation(
            "[CpfBackfill] Done — updated {Deputies} deputies, {Senators} senators",
            deputyCount, senatorCount);
    }

    // ── Federal Deputies ──────────────────────────────────────────────────────

    private async Task<int> BackfillDeputiesAsync(CancellationToken ct)
    {
        List<(int Id, string ExternalId, string Name)> deputies;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            deputies = await db.Politicians
                .Where(p => p.PoliticalPosition == "Federal Deputy"
                         && p.IsActive
                         && p.Cpf == null
                         && p.ExternalId != null)
                .Select(p => new { p.Id, p.ExternalId, p.FullName })
                .AsNoTracking()
                .ToListAsync(ct)
                .ContinueWith(t => t.Result
                    .Select(x => (x.Id, x.ExternalId!, x.FullName))
                    .ToList(), ct);
        }

        _logger.LogInformation("[CpfBackfill] Found {Count} deputies without CPF", deputies.Count);
        if (deputies.Count == 0) return 0;

        var client = _httpClientFactory.CreateClient("Chamber");
        var updated = 0;

        foreach (var (id, externalId, name) in deputies)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                using var resp = await client.GetAsync($"/api/v2/deputados/{externalId}", ct);
                if (!resp.IsSuccessStatusCode) continue;

                var json = await resp.Content.ReadAsStringAsync(ct);
                var detail = JsonSerializer.Deserialize<CamaraDeputadoDetail>(json, JsonOpts);
                var cpf = detail?.Dados?.Cpf?.Trim();
                if (string.IsNullOrWhiteSpace(cpf)) continue;

                // Normalize CPF: remove dots and dashes, pad to 11 digits
                cpf = new string(cpf.Where(char.IsDigit).ToArray()).PadLeft(11, '0');
                if (cpf.Length != 11) continue;

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
                var politician = await db.Politicians.FindAsync([id], ct);
                if (politician == null || politician.Cpf != null) continue;

                politician.Cpf = cpf;
                politician.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                updated++;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "[CpfBackfill] Deputy {Name} ({Id}): failed to fetch CPF", name, externalId);
            }

            await Task.Delay(100, ct); // Rate limiting
        }

        _logger.LogInformation("[CpfBackfill] Updated CPF on {Count}/{Total} federal deputies",
            updated, deputies.Count);
        return updated;
    }

    // ── Senators ──────────────────────────────────────────────────────────────

    private async Task<int> BackfillSenatorsAsync(CancellationToken ct)
    {
        List<(int Id, string ExternalId, string Name)> senators;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            senators = await db.Politicians
                .Where(p => p.PoliticalPosition == "Senator"
                         && p.IsActive
                         && p.Cpf == null
                         && p.ExternalId != null)
                .Select(p => new { p.Id, p.ExternalId, p.FullName })
                .AsNoTracking()
                .ToListAsync(ct)
                .ContinueWith(t => t.Result
                    .Select(x => (x.Id, x.ExternalId!, x.FullName))
                    .ToList(), ct);
        }

        _logger.LogInformation("[CpfBackfill] Found {Count} senators without CPF", senators.Count);
        if (senators.Count == 0) return 0;

        // Use ISenateScrapperService's HttpClient (already configured)
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(SenadoBase);
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ChecaAI-Worker/1.0");
        client.Timeout = TimeSpan.FromSeconds(15);

        var updated = 0;

        foreach (var (id, externalId, name) in senators)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                using var resp = await client.GetAsync(
                    $"/senador/{externalId}?v=5", ct);
                if (!resp.IsSuccessStatusCode) continue;

                var json = await resp.Content.ReadAsStringAsync(ct);
                var cpf = ExtractSenatorCpf(json);
                if (string.IsNullOrWhiteSpace(cpf)) continue;

                cpf = new string(cpf.Where(char.IsDigit).ToArray()).PadLeft(11, '0');
                if (cpf.Length != 11) continue;

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
                var politician = await db.Politicians.FindAsync([id], ct);
                if (politician == null || politician.Cpf != null) continue;

                politician.Cpf = cpf;
                politician.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                updated++;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "[CpfBackfill] Senator {Name} ({Id}): failed to fetch CPF", name, externalId);
            }

            await Task.Delay(150, ct);
        }

        _logger.LogInformation("[CpfBackfill] Updated CPF on {Count}/{Total} senators",
            updated, senators.Count);
        return updated;
    }

    /// <summary>
    /// Extracts CPF from the Senado dadosabertos JSON response.
    /// Tries multiple paths since the response shape varies.
    /// </summary>
    private static string? ExtractSenatorCpf(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Path 1: DetalheParlamentar.Parlamentar.DadosBasicosParlamentar.CpfParlamentar
            if (TryGetPath(root, out var val,
                    "DetalheParlamentar", "Parlamentar", "DadosBasicosParlamentar", "CpfParlamentar"))
                return val;

            // Path 2: DetalheParlamentar.Parlamentar.IdentificacaoParlamentar.CpfParlamentar
            if (TryGetPath(root, out val,
                    "DetalheParlamentar", "Parlamentar", "IdentificacaoParlamentar", "CpfParlamentar"))
                return val;

            // Path 3: Parlamentar.DadosBasicosParlamentar.CpfParlamentar
            if (TryGetPath(root, out val,
                    "Parlamentar", "DadosBasicosParlamentar", "CpfParlamentar"))
                return val;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetPath(JsonElement el, out string? value, params string[] path)
    {
        value = null;
        var current = el;
        foreach (var seg in path)
        {
            if (!current.TryGetProperty(seg, out current))
                return false;
        }
        value = current.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    private sealed class CamaraDeputadoDetail
    {
        [JsonPropertyName("dados")]
        public DeputadoDados? Dados { get; set; }
    }

    private sealed class DeputadoDados
    {
        [JsonPropertyName("cpf")]
        public string? Cpf { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nomeCivil")]
        public string? NomeCivil { get; set; }
    }
}
