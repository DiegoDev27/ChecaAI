using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Worker.Services;

/// <summary>
/// Syncs cabinet staff (assessores de gabinete) from Portal da Transparência CGU.
/// Fetches all civil servants of Câmara (orgaoExercicio=26242) and Senado (orgaoExercicio=20101),
/// then attempts to link each staff member to a politician by parsing the uorgExercicio field
/// (e.g. "GABINETE DO DEP. NOME DO DEPUTADO").
/// Requires CguApiKey in appsettings. Runs once at startup (after 15min), then every 24h.
/// </summary>
public class CguCabinetStaffSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CguCabinetStaffSyncService> _logger;

    private const string CguApiBase = "https://api.portaldatransparencia.gov.br/api-de-dados";
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(200);

    // CGU organ codes
    private static readonly (string OrgCode, string Label)[] Organs =
    [
        ("26242", "Câmara dos Deputados"),
        ("20101", "Senado Federal")
    ];

    // Patterns to extract politician name from uorgExercicio
    // e.g.: "GABINETE DO DEP. FULANO DE TAL" → "FULANO DE TAL"
    //       "GABINETE DO SEN. FULANO DE TAL" → "FULANO DE TAL"
    //       "GAB. DO DEP. FULANO DE TAL" → "FULANO DE TAL"
    private static readonly Regex[] GabinetePatterns =
    [
        new(@"GABINETE\s+DO?\s+(?:DEP\.|DEPUTADO|SEN\.|SENADOR)\s+(.+)", RegexOptions.IgnoreCase),
        new(@"GAB\.?\s+DO?\s+(?:DEP\.|DEPUTADO|SEN\.|SENADOR)\s+(.+)", RegexOptions.IgnoreCase),
    ];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public CguCabinetStaffSyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CguCabinetStaffSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var apiKey = _configuration["CguApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("[CguCabinetStaffSync] CguApiKey not configured — skipping staff sync. Register at portaldatransparencia.gov.br/api-de-dados");
            return;
        }

        _logger.LogInformation("[CguCabinetStaffSync] Service started — first run in {Delay}min", StartupDelay.TotalMinutes);
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncStaffAsync(apiKey, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CguCabinetStaffSync] Unexpected error during sync");
            }

            await Task.Delay(SyncInterval, stoppingToken);
        }
    }

    private async Task SyncStaffAsync(string apiKey, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Cgu");

        // Load all politicians for name matching (case-insensitive)
        Dictionary<string, int> politiciansByNormalizedName;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            var list = await db.Politicians
                .Where(p => p.IsActive)
                .Select(p => new { p.Id, p.FullName })
                .AsNoTracking()
                .ToListAsync(ct);

            politiciansByNormalizedName = list
                .GroupBy(p => NormalizeName(p.FullName))
                .ToDictionary(g => g.Key, g => g.First().Id);
        }

        var totalFetched = 0;
        var totalLinked = 0;
        var totalAdded = 0;

        foreach (var (orgCode, label) in Organs)
        {
            if (ct.IsCancellationRequested) break;

            _logger.LogInformation("[CguCabinetStaffSync] Fetching staff for {Label} (orgaoExercicio={Code})...", label, orgCode);

            var staffList = await FetchAllStaffAsync(client, orgCode, ct);
            totalFetched += staffList.Count;
            _logger.LogInformation("[CguCabinetStaffSync] Fetched {Count} staff from {Label}", staffList.Count, label);

            var (added, linked) = await PersistStaffAsync(staffList, politiciansByNormalizedName, ct);
            totalAdded += added;
            totalLinked += linked;
        }

        _logger.LogInformation("[CguCabinetStaffSync] Done — fetched {Total}, linked {Linked} to politicians, added {Added} new records",
            totalFetched, totalLinked, totalAdded);
    }

    private async Task<List<CguStaffRecord>> FetchAllStaffAsync(
        HttpClient client, string orgCode, CancellationToken ct)
    {
        var all = new List<CguStaffRecord>();
        var page = 1;
        // /servidores does not accept tamanhoPagina; returns a fixed page size (~500).
        // Correct param name is orgaoServidorExercicio (SIAPE code), not orgaoExercicio.

        while (true)
        {
            if (ct.IsCancellationRequested) break;

            var url = $"{CguApiBase}/servidores?orgaoServidorExercicio={orgCode}&situacaoServidor=1&pagina={page}";

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(url, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CguCabinetStaffSync] HTTP error fetching page {Page} for organ {Code}", page, orgCode);
                break;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogError("[CguCabinetStaffSync] Auth error {Status} — check CguApiKey", response.StatusCode);
                break;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[CguCabinetStaffSync] HTTP {Status} on page {Page} for organ {Code}", response.StatusCode, page, orgCode);
                break;
            }

            var json = await response.Content.ReadAsStringAsync(ct);

            // Detect AWS WAF CAPTCHA (200 OK with HTML body)
            if (json.TrimStart().StartsWith('<'))
            {
                _logger.LogWarning("[CguCabinetStaffSync] CGU API returned WAF/CAPTCHA HTML on page {Page} for organ {Code} — cabinet staff data unavailable from this environment. The Portal da Transparência blocks non-browser automated access.", page, orgCode);
                break;
            }

            List<CguStaffRecord>? batch;
            try
            {
                batch = JsonSerializer.Deserialize<List<CguStaffRecord>>(json, JsonOpts);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CguCabinetStaffSync] Parse error on page {Page}", page);
                break;
            }

            if (batch == null || batch.Count == 0)
                break;

            all.AddRange(batch);

            // The API uses a fixed internal page size (~500). When the batch is smaller,
            // we've reached the last page.
            if (batch.Count < 500)
                break;

            page++;
            await Task.Delay(RateLimitDelay, ct);
        }

        return all;
    }

    private async Task<(int Added, int Linked)> PersistStaffAsync(
        List<CguStaffRecord> staffList,
        Dictionary<string, int> politiciansByName,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        // Load existing ExternalIds to avoid duplicates
        var existingExternalIds = (await db.CabinetStaff
            .Where(s => s.ExternalId != null)
            .Select(s => s.ExternalId!)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var added = 0;
        var linked = 0;
        var unmatchedSamplesLogged = 0;
        var now = DateTime.UtcNow;
        var currentYear = now.Year;
        var currentMonth = now.Month;

        foreach (var record in staffList)
        {
            var extId = record.Id?.ToString() ?? record.Cpf;
            if (extId == null) continue;

            if (existingExternalIds.Contains(extId))
                continue;

            // Try to link to a politician via uorgExercicio name
            int? politicianId = TryMatchPolitician(record.UorgExercicioNome, politiciansByName);
            if (politicianId.HasValue)
            {
                linked++;
            }
            else if (unmatchedSamplesLogged < 10)
            {
                // Diagnostic: log a sample of raw uorgExercicio values so we can see the real
                // format CGU returns and tune the GabinetePatterns regex accordingly.
                _logger.LogInformation("[CguCabinetStaffSync] No match for uorgExercicio=\"{UorgExercicio}\" (nome={Nome})",
                    record.UorgExercicioNome, record.Nome);
                unmatchedSamplesLogged++;
            }

            var role = record.CargoNome?.Length > 100 ? record.CargoNome[..100] : record.CargoNome;
            var fullName = record.Nome?.Length > 300 ? record.Nome[..300] : record.Nome ?? "Desconhecido";
            var cpf = record.Cpf?.Length > 14 ? record.Cpf[..14] : record.Cpf;

            DateTime? startDate = null;
            if (DateTime.TryParse(record.DataInicioOcupacao, out var parsed))
                startDate = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

            db.CabinetStaff.Add(new CabinetStaff
            {
                PoliticianId = politicianId,
                FullName = fullName,
                Role = role,
                GrossSalary = 0,  // Salary detail requires separate /remuneracao call by CPF
                NetSalary = 0,
                StartDate = startDate,
                Cpf = cpf,
                ExternalId = extId.Length > 100 ? extId[..100] : extId,
                Source = "CGU",
                Month = currentMonth,
                Year = currentYear,
                CreatedAt = now
            });

            existingExternalIds.Add(extId);
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        return (added, linked);
    }

    /// <summary>
    /// Attempts to extract politician name from uorgExercicio and match it against known politicians.
    /// </summary>
    private static int? TryMatchPolitician(string? uorgExercicio, Dictionary<string, int> politiciansByName)
    {
        if (string.IsNullOrWhiteSpace(uorgExercicio))
            return null;

        foreach (var pattern in GabinetePatterns)
        {
            var match = pattern.Match(uorgExercicio);
            if (!match.Success) continue;

            var extractedName = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(extractedName)) continue;

            var normalized = NormalizeName(extractedName);

            // Exact normalized match
            if (politiciansByName.TryGetValue(normalized, out var id))
                return id;

            // Partial match: uorgExercicio name is contained in politician name or vice versa
            foreach (var (polName, polId) in politiciansByName)
            {
                if (polName.Contains(normalized) || normalized.Contains(polName))
                    return polId;
            }
        }

        return null;
    }

    private static string NormalizeName(string name) =>
        name.ToUpperInvariant()
            .Replace("'", "")
            .Replace("-", " ")
            .Trim();

    // ---- Internal DTOs ----

    private sealed class CguStaffRecord
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("nome")]
        public string? Nome { get; set; }

        [JsonPropertyName("cpf")]
        public string? Cpf { get; set; }

        // uorgExercicio can be an object { "nome": "..." } or a plain string depending on API version
        [JsonPropertyName("uorgExercicio")]
        public JsonElement UorgExercicioRaw { get; set; }

        [JsonPropertyName("cargo")]
        public JsonElement CargoRaw { get; set; }

        [JsonPropertyName("situacaoVinculo")]
        public JsonElement SituacaoVinculoRaw { get; set; }

        [JsonPropertyName("dataInicioOcupacao")]
        public string? DataInicioOcupacao { get; set; }

        /// <summary>Extracts nome from uorgExercicio regardless of whether it's a string or object.</summary>
        public string? UorgExercicioNome => ExtractStringOrObject(UorgExercicioRaw);

        /// <summary>Extracts nome from cargo.</summary>
        public string? CargoNome => ExtractStringOrObject(CargoRaw);

        private static string? ExtractStringOrObject(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Object when el.TryGetProperty("nome", out var n) => n.GetString(),
            _ => null
        };
    }
}
