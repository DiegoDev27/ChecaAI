using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Worker.Services;

/// <summary>
/// One-shot background service that seeds politicians of ALL non-federal levels
/// (Presidente, Governadores, Deputados Estaduais/Distritais, Prefeitos, Vereadores)
/// by downloading the official TSE bulk CSV files from the TSE CDN.
///
/// Source: https://cdn.tse.jus.br/estatistica/sead/odsele/consulta_cand/consulta_cand_{year}.zip
/// CSV is semicolon-delimited, ISO-8859-1 encoded.
///
/// Skips Senators and Federal Deputies (covered by dedicated sync services).
/// Runs once at startup; subsequent runs are idempotent (upsert by CPF).
/// </summary>
public class TsePoliticianSeedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TsePoliticianSeedService> _logger;
    private readonly HttpClient _httpClient;

    // TSE CDN bulk file URL pattern
    private const string TseCdnUrl =
        "https://cdn.tse.jus.br/estatistica/sead/odsele/consulta_cand/consulta_cand_{year}.zip";

    // CSV is ISO-8859-1 (Latin-1)
    private static readonly Encoding CsvEncoding = Encoding.GetEncoding("iso-8859-1");

    // Positions we CREATE new politicians for — federal levels have dedicated sync services
    private static readonly HashSet<string> SkippedCargosForCreate = new(StringComparer.OrdinalIgnoreCase)
    {
        "SENADOR", "DEPUTADO FEDERAL"
    };

    // Federal-level positions for which we UPDATE CPF on existing politicians
    private static readonly HashSet<string> FederalCargos = new(StringComparer.OrdinalIgnoreCase)
    {
        "SENADOR", "DEPUTADO FEDERAL"
    };

    private static readonly Dictionary<string, string> CargoMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PRESIDENTE"]        = "President",
        ["VICE-PRESIDENTE"]   = "President",
        ["GOVERNADOR"]        = "Governor",
        ["VICE-GOVERNADOR"]   = "Governor",
        ["DEPUTADO ESTADUAL"] = "State Deputy",
        ["DEPUTADO DISTRITAL"]= "State Deputy",
        ["PREFEITO"]          = "Mayor",
        ["VICE-PREFEITO"]     = "Mayor",
        ["VEREADOR"]          = "City Councilor",
    };

    public TsePoliticianSeedService(
        IServiceScopeFactory scopeFactory,
        ILogger<TsePoliticianSeedService> logger,
        HttpClient httpClient)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient = httpClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[TseSeed] Starting TSE politician seed via bulk CSV...");

        // 2022: federal elections (President, Governors, State Deputies)
        await SeedElectionYearAsync(2022, stoppingToken);

        // 2020: municipal elections (Mayors, City Councilors)
        await SeedElectionYearAsync(2020, stoppingToken);

        _logger.LogInformation("[TseSeed] Seed completed");
    }

    private async Task SeedElectionYearAsync(int year, CancellationToken ct)
    {
        var url = TseCdnUrl.Replace("{year}", year.ToString());
        _logger.LogInformation("[TseSeed] Downloading {Year} CSV from {Url}...", year, url);

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var zipStream = await response.Content.ReadAsStreamAsync(ct);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            // The ZIP contains one CSV per state + one BR file; we want the BR (national) file
            var entry = archive.Entries
                .FirstOrDefault(e => e.Name.Contains("BR", StringComparison.OrdinalIgnoreCase)
                                  && e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                          ?? archive.Entries.FirstOrDefault(e =>
                                  e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                _logger.LogWarning("[TseSeed] No CSV found in {Year} archive", year);
                return;
            }

            _logger.LogInformation("[TseSeed] Parsing {Entry} ({Size:N0} bytes compressed)...",
                entry.Name, entry.CompressedLength);

            var counts = await ParseAndPersistCsvAsync(entry, year, ct);

            if (counts.Count > 0)
            {
                var summary = string.Join(", ", counts.Select(kv => $"{kv.Key}: {kv.Value}"));
                _logger.LogInformation("[TseSeed] Year {Year} seeded — {Summary}", year, summary);
            }
            else
            {
                _logger.LogInformation("[TseSeed] Year {Year}: no new politicians to seed (already up to date)", year);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "[TseSeed] Failed to seed year {Year}", year);
        }
    }

    private async Task<Dictionary<string, int>> ParseAndPersistCsvAsync(
        ZipArchiveEntry entry, int year, CancellationToken ct)
    {
        var totalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await using var csvStream = entry.Open();
        using var reader = new StreamReader(csvStream, CsvEncoding);

        // Parse header to find column indices
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null) return totalCounts;

        var headers = headerLine.Split(';');
        var idx = BuildColumnIndex(headers);

        if (!idx.IsValid)
        {
            _logger.LogWarning("[TseSeed] CSV header doesn't match expected columns. Found: {Headers}",
                string.Join(", ", headers.Take(10)));
            return totalCounts;
        }

        // Stream through the CSV in batches to avoid loading everything in memory
        const int batchSize = 500;
        var batch = new List<TseCandidatoRow>(batchSize);
        var federalCpfBatch = new List<TseCandidatoRow>(); // for CPF backfill on existing senators/deputies

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null && !ct.IsCancellationRequested)
        {
            var row = ParseRow(line, idx);
            if (row == null) continue;

            // Only keep elected candidates
            if (!row.DsSituacaoTurno.Contains("ELEITO", StringComparison.OrdinalIgnoreCase)) continue;

            if (FederalCargos.Contains(row.DsCargo))
            {
                // Collect for CPF update on existing politicians
                if (!string.IsNullOrEmpty(row.Cpf))
                    federalCpfBatch.Add(row);
                continue;
            }

            if (!CargoMap.ContainsKey(row.DsCargo)) continue;
            if (SkippedCargosForCreate.Contains(row.DsCargo)) continue;

            batch.Add(row);

            if (batch.Count >= batchSize)
            {
                var batchCounts = await PersistBatchAsync(batch, ct);
                MergeCounts(totalCounts, batchCounts);
                batch.Clear();
            }
        }

        // Flush remaining create batch
        if (batch.Count > 0)
        {
            var batchCounts = await PersistBatchAsync(batch, ct);
            MergeCounts(totalCounts, batchCounts);
        }

        // Update CPF on existing senators/deputies
        if (federalCpfBatch.Count > 0)
        {
            var updated = await UpdateFederalCpfsAsync(federalCpfBatch, ct);
            if (updated > 0)
                _logger.LogInformation("[TseSeed] Year {Year}: updated CPF on {N} existing senators/deputies", year, updated);
        }

        return totalCounts;
    }

    private async Task<Dictionary<string, int>> PersistBatchAsync(
        List<TseCandidatoRow> rows, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        var cpfs = rows.Select(r => r.Cpf).Where(c => !string.IsNullOrEmpty(c)).ToHashSet();

        var existingCpfList = await db.Politicians
            .Where(p => cpfs.Contains(p.Cpf))
            .Select(p => p.Cpf)
            .ToListAsync(ct);
        var existingCpfs = existingCpfList.Where(c => c != null).Select(c => c!).ToHashSet();

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var added = new HashSet<string>(); // deduplicate within batch (vice + titular same CPF)

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.Cpf)) continue;
            if (existingCpfs.Contains(row.Cpf)) continue;
            if (!added.Add(row.Cpf)) continue; // duplicate in this batch

            if (!CargoMap.TryGetValue(row.DsCargo, out var position)) continue;

            var politician = new Politician
            {
                ExternalId = !string.IsNullOrEmpty(row.SqCandidato)
                    ? $"tse-{row.SqCandidato}"
                    : $"tse-cpf-{row.Cpf}",
                FullName = ToTitleCase(row.NmCandidato),
                PoliticalPosition = position,
                Party = row.SgPartido?.Trim(),
                State = NormalizeUf(row.SgUf),
                City = position is "Mayor" or "City Councilor"
                    ? ToTitleCase(row.NmMunicipio)
                    : null,
                Cpf = row.Cpf,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Politicians.Add(politician);
            counts[position] = counts.GetValueOrDefault(position) + 1;
        }

        if (counts.Count > 0)
            await db.SaveChangesAsync(ct);

        return counts;
    }

    /// <summary>
    /// Matches TSE senator/deputy rows to existing Politicians by normalized name + state
    /// and backfills their CPF where it is currently null.
    /// </summary>
    private async Task<int> UpdateFederalCpfsAsync(List<TseCandidatoRow> rows, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        // Load senators and deputies that have no CPF yet
        var existing = await db.Politicians
            .Where(p => p.Cpf == null
                     && (p.PoliticalPosition == "Senator" || p.PoliticalPosition == "Federal Deputy"))
            .ToListAsync(ct);

        if (existing.Count == 0) return 0;

        // Build lookup: NormalizedName|State|Position → politician
        var lookup = new Dictionary<string, Politician>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in existing)
        {
            var key = MakeLookupKey(p.FullName, p.State, p.PoliticalPosition);
            lookup.TryAdd(key, p);

            // Also index by first+last word only (handles middle-name mismatches)
            var shortKey = MakeShortLookupKey(p.FullName, p.State, p.PoliticalPosition);
            if (shortKey != key)
                lookup.TryAdd(shortKey, p);
        }

        var updated = 0;

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.Cpf)) continue;

            var position = row.DsCargo.Equals("SENADOR", StringComparison.OrdinalIgnoreCase)
                ? "Senator" : "Federal Deputy";

            var key = MakeLookupKey(row.NmCandidato, row.SgUf, position);
            if (!lookup.TryGetValue(key, out var match))
            {
                var shortKey = MakeShortLookupKey(row.NmCandidato, row.SgUf, position);
                lookup.TryGetValue(shortKey, out match);
            }

            if (match == null || match.Cpf != null) continue;

            match.Cpf = row.Cpf;
            match.UpdatedAt = DateTime.UtcNow;
            updated++;
        }

        if (updated > 0)
            await db.SaveChangesAsync(ct);

        return updated;
    }

    private static string MakeLookupKey(string? name, string? state, string? position)
    {
        var n = (name ?? "").ToUpperInvariant().Trim();
        var s = (state ?? "").ToUpperInvariant().Trim();
        var p = (position ?? "").ToUpperInvariant().Trim();
        return $"{n}|{s}|{p}";
    }

    private static string MakeShortLookupKey(string? name, string? state, string? position)
    {
        var parts = (name ?? "").ToUpperInvariant().Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var shortName = parts.Length >= 2 ? $"{parts[0]} {parts[^1]}" : (name ?? "").ToUpperInvariant().Trim();
        var s = (state ?? "").ToUpperInvariant().Trim();
        var p = (position ?? "").ToUpperInvariant().Trim();
        return $"{shortName}|{s}|{p}";
    }

    // ── CSV helpers ───────────────────────────────────────────────────────────

    private static ColumnIndex BuildColumnIndex(string[] headers)
    {
        var idx = new ColumnIndex();
        for (var i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim('"').Trim().ToUpperInvariant();
            switch (h)
            {
                case "NM_CANDIDATO":      idx.NmCandidato = i; break;
                case "NR_CPF_CANDIDATO":  idx.NrCpfCandidato = i; break;
                case "DS_CARGO":          idx.DsCargo = i; break;
                case "SG_PARTIDO":        idx.SgPartido = i; break;
                case "SG_UF":             idx.SgUf = i; break;
                case "NM_MUNICIPIO":      idx.NmMunicipio = i; break;
                case "SQ_CANDIDATO":      idx.SqCandidato = i; break;
                case "DS_SIT_TOT_TURNO":  idx.DsSituacaoTurno = i; break;
            }
        }
        return idx;
    }

    private static TseCandidatoRow? ParseRow(string line, ColumnIndex idx)
    {
        // TSE CSVs use ';' separator, values may be quoted
        var cols = SplitCsv(line);
        var max = cols.Length - 1;

        string Get(int i) => i >= 0 && i <= max ? cols[i].Trim('"').Trim() : "";

        var cargo = Get(idx.DsCargo).ToUpperInvariant();
        if (string.IsNullOrEmpty(cargo)) return null;

        var cpfRaw = Get(idx.NrCpfCandidato);
        var cpf = cpfRaw.Replace(".", "").Replace("-", "").Replace("/", "").Trim();
        if (cpf.Length < 11) cpf = cpf.PadLeft(11, '0'); // TSE sometimes omits leading zeros

        return new TseCandidatoRow
        {
            NmCandidato    = Get(idx.NmCandidato),
            Cpf            = cpf,
            DsCargo        = cargo,
            SgPartido      = Get(idx.SgPartido),
            SgUf           = Get(idx.SgUf),
            NmMunicipio    = Get(idx.NmMunicipio),
            SqCandidato    = Get(idx.SqCandidato),
            DsSituacaoTurno= Get(idx.DsSituacaoTurno)
        };
    }

    /// <summary>Simple CSV split on ';' — handles basic quoted fields.</summary>
    private static string[] SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"')  { inQuotes = !inQuotes; continue; }
            if (c == ';' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(c);
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }

    private static string? NormalizeUf(string? uf)
    {
        if (string.IsNullOrWhiteSpace(uf)) return null;
        return uf.Trim().Length == 2 ? uf.Trim().ToUpperInvariant() : null;
    }

    private static string ToTitleCase(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name ?? "";
        return System.Globalization.CultureInfo.GetCultureInfo("pt-BR")
            .TextInfo.ToTitleCase(name.ToLowerInvariant());
    }

    private static void MergeCounts(Dictionary<string, int> target, Dictionary<string, int> source)
    {
        foreach (var (k, v) in source)
            target[k] = target.GetValueOrDefault(k) + v;
    }

    // ── Private nested types ─────────────────────────────────────────────────

    private sealed class TseCandidatoRow
    {
        public string NmCandidato     { get; set; } = "";
        public string Cpf             { get; set; } = "";
        public string DsCargo         { get; set; } = "";
        public string? SgPartido      { get; set; }
        public string? SgUf           { get; set; }
        public string? NmMunicipio    { get; set; }
        public string? SqCandidato    { get; set; }
        public string DsSituacaoTurno { get; set; } = "";
    }

    private sealed class ColumnIndex
    {
        public int NmCandidato     = -1;
        public int NrCpfCandidato  = -1;
        public int DsCargo         = -1;
        public int SgPartido       = -1;
        public int SgUf            = -1;
        public int NmMunicipio     = -1;
        public int SqCandidato     = -1;
        public int DsSituacaoTurno = -1;

        public bool IsValid =>
            NmCandidato >= 0 && NrCpfCandidato >= 0 && DsCargo >= 0 && DsSituacaoTurno >= 0;
    }
}
