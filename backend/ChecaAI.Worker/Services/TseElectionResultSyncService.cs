using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Worker.Services;

/// <summary>
/// Seeds ElectionResults from TSE bulk CSV files (votacao_candidato_munzona).
/// The file has one row per candidate × municipality × zone, so votes are aggregated
/// in memory before persisting. Only politicians already in the DB are processed.
///
/// Source URL pattern:
///   https://cdn.tse.jus.br/estatistica/sead/odsele/votacao_candidato_munzona/
///   votacao_candidato_munzona_{year}_BR.zip
///
/// Runs once at startup (idempotent per election year). Repeats never — just restart
/// the worker to re-process if needed.
/// </summary>
public class TseElectionResultSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TseElectionResultSyncService> _logger;

    private static readonly Encoding CsvEncoding = Encoding.GetEncoding("iso-8859-1");
    private static readonly int[] ElectionYears = [2022, 2020, 2018];

    private const string VotacaoCdnUrl =
        "https://cdn.tse.jus.br/estatistica/sead/odsele/votacao_candidato_munzona/" +
        "votacao_candidato_munzona_{year}.zip";

    // DS_SIT_TOT_TURNO values that mean the candidate was elected
    private static readonly HashSet<string> ElectedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ELEITO", "ELEITO POR QP", "ELEITO POR MÉDIA", "ELEITA", "ELEITA POR QP", "ELEITA POR MÉDIA"
    };

    // DS_CARGO → PoliticalPosition mapping (matches what TsePoliticianSeedService uses)
    private static readonly Dictionary<string, string> CargoToPosition =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PRESIDENTE"] = "President",
            ["VICE-PRESIDENTE"] = "President",
            ["GOVERNADOR"] = "Governor",
            ["VICE-GOVERNADOR"] = "Governor",
            ["SENADOR"] = "Senator",
            ["DEPUTADO FEDERAL"] = "Federal Deputy",
            ["DEPUTADO ESTADUAL"] = "State Deputy",
            ["DEPUTADO DISTRITAL"] = "State Deputy",
            ["PREFEITO"] = "Mayor",
            ["VICE-PREFEITO"] = "Mayor",
            ["VEREADOR"] = "City Councilor",
        };

    public TseElectionResultSyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<TseElectionResultSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger startup so other services don't compete
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        if (stoppingToken.IsCancellationRequested) return;

        _logger.LogInformation("[TseElectionResult] Starting TSE election result seed...");

        var client = _httpClientFactory.CreateClient("TseSeed");

        foreach (var year in ElectionYears)
        {
            if (stoppingToken.IsCancellationRequested) break;

            await SeedElectionResultsAsync(client, year, stoppingToken);

            // Small pause between years to avoid hammering CDN
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("[TseElectionResult] Seed completed");
    }

    private async Task SeedElectionResultsAsync(HttpClient client, int year, CancellationToken ct)
    {
        // Idempotency check
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            var already = await db.ElectionResults.AnyAsync(r => r.ElectionYear == year, ct);
            if (already)
            {
                _logger.LogInformation("[TseElectionResult] ElectionResults for {Year} already seeded — skipping", year);
                return;
            }
        }

        // Build politician lookup
        var lookup = await BuildLookupAsync(ct);
        if (lookup.Count == 0)
        {
            _logger.LogWarning("[TseElectionResult] No politicians found in DB — skipping {Year}", year);
            return;
        }

        var url = VotacaoCdnUrl.Replace("{year}", year.ToString());
        _logger.LogInformation("[TseElectionResult] Downloading votacao_candidato_munzona {Year}...", year);

        // The download + ZIP read below can fail mid-stream (huge file, CDN drops the
        // connection) well after TryGetAsync's own try/catch has returned successfully.
        // Must not let that escape uncaught — HostOptions.BackgroundServiceExceptionBehavior
        // defaults to StopHost, so an unhandled exception here kills the ENTIRE Worker
        // process (every other sync service included), not just this one's seed for {year}.
        try
        {
            using var response = await TryGetAsync(client, url, ct);
            if (response == null) return;

            await using var zipStream = await response.Content.ReadAsStreamAsync(ct);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var entry = FindCsvEntry(archive);
            if (entry == null)
            {
                _logger.LogWarning("[TseElectionResult] No CSV found in archive for {Year}", year);
                return;
            }

            _logger.LogInformation("[TseElectionResult] Parsing {Entry} for {Year}...", entry.Name, year);
            var results = await AggregateResultsAsync(entry, year, lookup, ct);

            if (results.Count == 0)
            {
                _logger.LogWarning("[TseElectionResult] No linked results found for {Year}", year);
                return;
            }

            await PersistResultsAsync(results, ct);
            _logger.LogInformation(
                "[TseElectionResult] {Year}: persisted {Count} election results ({Elected} elected)",
                year, results.Count, results.Count(r => r.IsElected));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "[TseElectionResult] Failed to seed election results for {Year} — skipping", year);
        }
    }

    // ── Aggregation ───────────────────────────────────────────────────────────

    private async Task<List<ElectionResult>> AggregateResultsAsync(
        ZipArchiveEntry entry, int year,
        Dictionary<string, int> lookup,   // key → politicianId
        CancellationToken ct)
    {
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream, CsvEncoding);

        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null) return [];

        var headers = headerLine.Split(';');

        int sqIdx = -1, cpfIdx = -1, cargoIdx = -1, ufIdx = -1, munIdx = -1,
            votosIdx = -1, sitIdx = -1, turnoIdx = -1;

        for (var i = 0; i < headers.Length; i++)
        {
            var h = NormalizeHeader(headers[i]);
            switch (h)
            {
                case "SQ_CANDIDATO":                    sqIdx = i;    break;
                case "NR_CPF_CANDIDATO":                cpfIdx = i;   break;
                case "DS_CARGO":                        cargoIdx = i; break;
                case "SG_UF":                           ufIdx = i;    break;
                case "NM_UE":                           munIdx = i;   break;
                case "QT_VOTOS_NOMINAIS_VALIDOS":       votosIdx = i; break;
                case "QT_VOTOS_NOMINAIS":
                    if (votosIdx < 0) votosIdx = i;     // fallback
                    break;
                case "DS_SIT_TOT_TURNO":                sitIdx = i;   break;
                case "NR_TURNO":                        turnoIdx = i; break;
            }
        }

        if (sqIdx < 0 || cargoIdx < 0)
        {
            _logger.LogWarning("[TseElectionResult] CSV missing expected columns (SQ_CANDIDATO / DS_CARGO)");
            return [];
        }

        // politicianId → accumulated result
        var acc = new Dictionary<int, ElectionResult>();

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null && !ct.IsCancellationRequested)
        {
            var cols = SplitCsv(line);
            string Get(int i) => i >= 0 && i < cols.Length ? cols[i].Trim('"').Trim() : "";

            // Only process 1st round (NR_TURNO == "1") to avoid double-counting runoffs
            if (turnoIdx >= 0 && Get(turnoIdx) != "1") continue;

            var sqCandidato = Get(sqIdx);
            var cpfRaw = NormalizeCpf(Get(cpfIdx));

            // Build lookup keys
            var tseKey = $"tse-{sqCandidato}";
            if (!lookup.TryGetValue(tseKey, out var politicianId))
            {
                if (string.IsNullOrWhiteSpace(cpfRaw) || !lookup.TryGetValue($"cpf-{cpfRaw}", out politicianId))
                    continue;
            }

            var votosStr = Get(votosIdx);
            var votos = ParseLong(votosStr);
            var cargo = Get(cargoIdx);
            var uf = Get(ufIdx).ToUpperInvariant();
            var municipio = Get(munIdx);
            var sit = Get(sitIdx);
            var isElected = ElectedStatuses.Contains(sit);
            var position = CargoToPosition.TryGetValue(cargo, out var pos) ? pos : cargo;

            if (acc.TryGetValue(politicianId, out var existing))
            {
                existing.VotesReceived += votos;
                // If any record says elected, treat as elected
                if (isElected) existing.IsElected = true;
            }
            else
            {
                acc[politicianId] = new ElectionResult
                {
                    PoliticianId = politicianId,
                    ElectionYear = year,
                    Position = position.Length > 50 ? position[..50] : position,
                    State = uf.Length > 2 ? null : uf,
                    City = municipio.Length > 100 ? municipio[..100] : (string.IsNullOrWhiteSpace(municipio) ? null : municipio),
                    VotesReceived = votos,
                    TotalVotes = 0,   // Not available in this file without full-race aggregation
                    VoteShare = 0,
                    IsElected = isElected,
                    ExternalId = sqCandidato.Length > 50 ? null : sqCandidato,
                    CreatedAt = DateTime.UtcNow
                };
            }
        }

        return [.. acc.Values];
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private async Task PersistResultsAsync(List<ElectionResult> results, CancellationToken ct)
    {
        const int batchSize = 300;
        for (var i = 0; i < results.Count; i += batchSize)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            var batch = results.Skip(i).Take(batchSize).ToList();
            db.ElectionResults.AddRange(batch);
            await db.SaveChangesAsync(ct);
        }
    }

    // ── Lookup builder ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a flat dictionary with two kinds of keys:
    ///   "tse-{SQ_CANDIDATO}"  → politicianId   (for TSE-seeded politicians)
    ///   "cpf-{cpf11digits}"   → politicianId   (for senators/federal deputies)
    /// </summary>
    private async Task<Dictionary<string, int>> BuildLookupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        var list = await db.Politicians
            .Where(p => p.ExternalId != null || p.Cpf != null)
            .Select(p => new { p.Id, p.ExternalId, p.Cpf })
            .AsNoTracking()
            .ToListAsync(ct);

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in list)
        {
            if (!string.IsNullOrWhiteSpace(p.ExternalId))
                result.TryAdd($"tse-{p.ExternalId}", p.Id);
            if (!string.IsNullOrWhiteSpace(p.Cpf))
                result.TryAdd($"cpf-{p.Cpf}", p.Id);
        }

        // Also index by raw ExternalId number (without "tse-" prefix) in case it was stored differently
        foreach (var p in list)
        {
            if (!string.IsNullOrWhiteSpace(p.ExternalId))
            {
                var raw = p.ExternalId.StartsWith("tse-", StringComparison.OrdinalIgnoreCase)
                    ? p.ExternalId[4..]
                    : p.ExternalId;
                result.TryAdd($"tse-{raw}", p.Id);
            }
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage?> TryGetAsync(HttpClient client, string url, CancellationToken ct)
    {
        try
        {
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("[TseElectionResult] File not found: {Url}", url);
                return null;
            }
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TseElectionResult] Failed to download {Url}", url);
            return null;
        }
    }

    private static ZipArchiveEntry? FindCsvEntry(ZipArchive archive)
        => archive.Entries
               .FirstOrDefault(e => e.Name.Contains("BR", StringComparison.OrdinalIgnoreCase)
                                 && e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
           ?? archive.Entries
               .FirstOrDefault(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

    private static string NormalizeHeader(string h)
        => h.Trim('"').Trim().ToUpperInvariant();

    private static long ParseLong(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0L;
        s = s.Trim().Replace(".", "").Replace(",", "");
        return long.TryParse(s, out var v) ? v : 0L;
    }

    private static string NormalizeCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return "";
        cpf = cpf.Replace(".", "").Replace("-", "").Replace("/", "").Trim();
        return cpf.Length < 11 ? cpf.PadLeft(11, '0') : cpf;
    }

    private static string[] SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == ';' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(c);
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }
}
