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
/// Seeds CampaignExpenses and AssetDeclarations from TSE bulk CSV files.
/// Sources (TSE CDN):
///   Bens:    https://cdn.tse.jus.br/estatistica/sead/odsele/bem_candidato/bem_candidato_{year}_BR.zip
///   Despesas: https://cdn.tse.jus.br/estatistica/sead/odsele/prestacao_contas/despesas_contratadas_candidatos_{year}_BR.zip
/// CSV format: semicolon-delimited, ISO-8859-1, header row.
/// Links to Politicians via ExternalId ("tse-{SQ_CANDIDATO}") for non-federal politicians,
/// or via CPF for senators/federal deputies if CPF is present in the row.
/// Runs once at startup (idempotent). Skips years already fully seeded.
/// </summary>
public class TseTransparencySyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TseTransparencySyncService> _logger;

    private static readonly Encoding CsvEncoding = Encoding.GetEncoding("iso-8859-1");
    private static readonly int[] ElectionYears = [2022, 2020, 2018];

    // CDN URL patterns — TSE removed the _BR suffix from filenames in 2024.
    // For expenses, the separate despesas_contratadas file was merged into the
    // consolidated candidate accounts ZIP.
    private const string BemCdnUrl =
        "https://cdn.tse.jus.br/estatistica/sead/odsele/bem_candidato/bem_candidato_{year}.zip";
    private const string DespesasCdnUrl =
        "https://cdn.tse.jus.br/estatistica/sead/odsele/prestacao_contas/prestacao_de_contas_eleitorais_candidatos_{year}.zip";

    public TseTransparencySyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<TseTransparencySyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for DB to be ready and other services to start
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        if (stoppingToken.IsCancellationRequested) return;

        _logger.LogInformation("[TseTransparency] Starting TSE transparency seed (campaign expenses + asset declarations)...");

        var client = _httpClientFactory.CreateClient("TseSeed");

        foreach (var year in ElectionYears)
        {
            if (stoppingToken.IsCancellationRequested) break;

            // Build politician lookup for this scope
            var lookup = await BuildPoliticianLookupAsync(stoppingToken);

            await SeedAssetDeclarationsAsync(client, year, lookup, stoppingToken);
            await SeedCampaignExpensesAsync(client, year, lookup, stoppingToken);
        }

        _logger.LogInformation("[TseTransparency] Seed completed");
    }

    // ── Asset Declarations (Bens de Candidatos) ──────────────────────────────

    private async Task SeedAssetDeclarationsAsync(
        HttpClient client, int year, PoliticianLookup lookup, CancellationToken ct)
    {
        // Check if already seeded for this year
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            var already = await db.AssetDeclarations.AnyAsync(a => a.ElectionYear == year, ct);
            if (already)
            {
                _logger.LogInformation("[TseTransparency] AssetDeclarations for {Year} already seeded — skipping", year);
                return;
            }
        }

        var url = BemCdnUrl.Replace("{year}", year.ToString());
        _logger.LogInformation("[TseTransparency] Downloading bem_candidato {Year} from CDN...", year);

        using var response = await TryGetAsync(client, url, ct);
        if (response == null) return;

        await using var zipStream = await response.Content.ReadAsStreamAsync(ct);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = FindCsvEntry(archive, year);
        if (entry == null)
        {
            _logger.LogWarning("[TseTransparency] No CSV found in bem_candidato {Year} archive", year);
            return;
        }

        _logger.LogInformation("[TseTransparency] Parsing {Entry} for asset declarations...", entry.Name);
        var (total, linked) = await ParseBemCandidatoAsync(entry, year, lookup, ct);
        _logger.LogInformation("[TseTransparency] AssetDeclarations {Year}: added {Total} records, linked to {Linked} politicians", year, total, linked);
    }

    private async Task<(int Total, int Linked)> ParseBemCandidatoAsync(
        ZipArchiveEntry entry, int year, PoliticianLookup lookup, CancellationToken ct)
    {
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream, CsvEncoding);

        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null) return (0, 0);

        var headers = headerLine.Split(';');
        int sqIdx = -1, tipoIdx = -1, descIdx = -1, valorIdx = -1, cpfIdx = -1, nmIdx = -1;

        for (var i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim('"').Trim().ToUpperInvariant();
            switch (h)
            {
                case "SQ_CANDIDATO":
                case "SQ_CAND":            sqIdx = i; break;
                case "DS_TIPO_BEM":
                case "DS_TIPO_BEM_CANDIDATO": tipoIdx = i; break;
                case "DS_BEM":
                case "DS_BEM_CANDIDATO":   descIdx = i; break;
                case "VR_BEM_CANDIDATO":
                case "VR_BEM":             valorIdx = i; break;
                case "NR_CPF_CANDIDATO":
                case "NR_CPF":             cpfIdx = i; break;
                case "NM_CANDIDATO":
                case "NM_CAND":            nmIdx = i; break;
            }
        }

        if (sqIdx < 0 || tipoIdx < 0)
        {
            _logger.LogWarning("[TseTransparency] bem_candidato {Year} CSV missing expected columns", year);
            return (0, 0);
        }

        const int batchSize = 500;
        var batch = new List<AssetDeclaration>(batchSize);
        var seenKeys = new HashSet<string>(); // SQ_CANDIDATO + tipo for dedup within run
        var totalAdded = 0;
        var totalLinked = 0;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null && !ct.IsCancellationRequested)
        {
            var cols = SplitCsv(line);
            string Get(int i) => i >= 0 && i < cols.Length ? cols[i].Trim('"').Trim() : "";

            var sqCandidato = Get(sqIdx);
            var cpfRaw = NormalizeCpf(Get(cpfIdx));
            var tipoRaw = Get(tipoIdx);
            var desc = Get(descIdx);
            var valorStr = Get(valorIdx);

            if (string.IsNullOrWhiteSpace(sqCandidato) && string.IsNullOrWhiteSpace(cpfRaw)) continue;

            var politicianId = lookup.FindPolitician(sqCandidato, cpfRaw);
            if (!politicianId.HasValue) continue;

            var dedupeKey = $"{politicianId}-{year}-{tipoRaw}-{valorStr}";
            if (!seenKeys.Add(dedupeKey)) continue;

            var amount = ParseDecimal(valorStr);
            var tipo = tipoRaw.Length > 100 ? tipoRaw[..100] : tipoRaw;
            var description = desc.Length > 500 ? desc[..500] : desc;

            batch.Add(new AssetDeclaration
            {
                PoliticianId = politicianId.Value,
                ElectionYear = year,
                AssetType = string.IsNullOrWhiteSpace(tipo) ? "Outros" : tipo,
                DeclaredValue = amount,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                ExternalId = $"{sqCandidato}-{seenKeys.Count}".Length > 50
                    ? null
                    : $"{sqCandidato}-{seenKeys.Count}",
                CreatedAt = DateTime.UtcNow
            });
            totalLinked++;

            if (batch.Count >= batchSize)
            {
                await PersistBatchAsync<AssetDeclaration>(batch, ct);
                totalAdded += batch.Count;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await PersistBatchAsync<AssetDeclaration>(batch, ct);
            totalAdded += batch.Count;
        }

        return (totalAdded, totalLinked);
    }

    // ── Campaign Expenses (Despesas Contratadas) ──────────────────────────────

    private async Task SeedCampaignExpensesAsync(
        HttpClient client, int year, PoliticianLookup lookup, CancellationToken ct)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            var already = await db.CampaignExpenses.AnyAsync(e => e.ElectionYear == year, ct);
            if (already)
            {
                _logger.LogInformation("[TseTransparency] CampaignExpenses for {Year} already seeded — skipping", year);
                return;
            }
        }

        var url = DespesasCdnUrl.Replace("{year}", year.ToString());
        _logger.LogInformation("[TseTransparency] Downloading despesas_contratadas {Year} from CDN...", year);

        using var response = await TryGetAsync(client, url, ct);
        if (response == null) return;

        await using var zipStream = await response.Content.ReadAsStreamAsync(ct);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = FindCsvEntry(archive, year, "despesas_contratadas");
        if (entry == null)
        {
            _logger.LogWarning("[TseTransparency] No CSV found in despesas_contratadas {Year} archive", year);
            return;
        }

        _logger.LogInformation("[TseTransparency] Parsing {Entry} for campaign expenses...", entry.Name);
        var (total, linked) = await ParseDespesasAsync(entry, year, lookup, ct);
        _logger.LogInformation("[TseTransparency] CampaignExpenses {Year}: added {Total} records, linked to {Linked} politicians", year, total, linked);
    }

    private async Task<(int Total, int Linked)> ParseDespesasAsync(
        ZipArchiveEntry entry, int year, PoliticianLookup lookup, CancellationToken ct)
    {
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream, CsvEncoding);

        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null) return (0, 0);

        var headers = headerLine.Split(';');
        int sqIdx = -1, origemIdx = -1, descIdx = -1, valorIdx = -1, fornecedorIdx = -1, cnpjIdx = -1, cpfIdx = -1;

        for (var i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim('"').Trim().ToUpperInvariant();
            switch (h)
            {
                case "SQ_CANDIDATO":                sqIdx = i; break;
                case "DS_ORIGEM_DESPESA":           origemIdx = i; break;
                case "DS_DESPESA":                  descIdx = i; break;
                case "VR_DESPESA_CONTRATADA":       valorIdx = i; break;
                case "NM_FORNECEDOR":               fornecedorIdx = i; break;
                case "NR_CNPJ_CPF_FORNECEDOR":      cnpjIdx = i; break;
                case "NR_CPF_CANDIDATO":            cpfIdx = i; break;
            }
        }

        if (sqIdx < 0 || valorIdx < 0)
        {
            _logger.LogWarning("[TseTransparency] despesas {Year} CSV missing expected columns", year);
            return (0, 0);
        }

        const int batchSize = 500;
        var batch = new List<CampaignExpense>(batchSize);
        var totalAdded = 0;
        var totalLinked = 0;
        var counter = 0;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null && !ct.IsCancellationRequested)
        {
            var cols = SplitCsv(line);
            string Get(int i) => i >= 0 && i < cols.Length ? cols[i].Trim('"').Trim() : "";

            var sqCandidato = Get(sqIdx);
            var cpfRaw = NormalizeCpf(Get(cpfIdx));
            var origem = Get(origemIdx);
            var desc = Get(descIdx);
            var valorStr = Get(valorIdx);
            var fornecedor = Get(fornecedorIdx);
            var cnpj = Get(cnpjIdx);

            if (string.IsNullOrWhiteSpace(sqCandidato) && string.IsNullOrWhiteSpace(cpfRaw)) continue;

            var politicianId = lookup.FindPolitician(sqCandidato, cpfRaw);
            if (!politicianId.HasValue) continue;

            counter++;
            var category = string.IsNullOrWhiteSpace(origem) ? desc : origem;
            category = category.Length > 100 ? category[..100] : category;

            batch.Add(new CampaignExpense
            {
                PoliticianId = politicianId.Value,
                ElectionYear = year,
                Category = string.IsNullOrWhiteSpace(category) ? "Outros" : category,
                Amount = ParseDecimal(valorStr),
                Provider = fornecedor.Length > 200 ? fornecedor[..200] : (string.IsNullOrWhiteSpace(fornecedor) ? null : fornecedor),
                ProviderCnpj = cnpj.Length > 20 ? cnpj[..20] : (string.IsNullOrWhiteSpace(cnpj) ? null : cnpj),
                Description = desc.Length > 500 ? desc[..500] : (string.IsNullOrWhiteSpace(desc) ? null : desc),
                ExternalId = $"{sqCandidato}-{counter}".Length > 50 ? null : $"{sqCandidato}-{counter}",
                CreatedAt = DateTime.UtcNow
            });
            totalLinked++;

            if (batch.Count >= batchSize)
            {
                await PersistBatchAsync<CampaignExpense>(batch, ct);
                totalAdded += batch.Count;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await PersistBatchAsync<CampaignExpense>(batch, ct);
            totalAdded += batch.Count;
        }

        return (totalAdded, totalLinked);
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private async Task<PoliticianLookup> BuildPoliticianLookupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        // Load all politicians: ExternalId and CPF for lookup
        var list = await db.Politicians
            .Where(p => p.ExternalId != null || p.Cpf != null)
            .Select(p => new { p.Id, p.ExternalId, p.Cpf })
            .AsNoTracking()
            .ToListAsync(ct);

        var tseKeyToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var cpfToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in list)
        {
            if (!string.IsNullOrWhiteSpace(p.ExternalId))
                tseKeyToId.TryAdd(p.ExternalId, p.Id);
            if (!string.IsNullOrWhiteSpace(p.Cpf))
                cpfToId.TryAdd(p.Cpf, p.Id);
        }

        return new PoliticianLookup(tseKeyToId, cpfToId);
    }

    private async Task PersistBatchAsync<T>(List<T> batch, CancellationToken ct) where T : class
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
        db.Set<T>().AddRange(batch);
        await db.SaveChangesAsync(ct);
    }

    private async Task<HttpResponseMessage?> TryGetAsync(HttpClient client, string url, CancellationToken ct)
    {
        try
        {
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("[TseTransparency] File not found at {Url} — skipping", url);
                return null;
            }
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TseTransparency] Failed to download {Url}", url);
            return null;
        }
    }

    private static ZipArchiveEntry? FindCsvEntry(ZipArchive archive, int year, string? keyword = null)
    {
        var csvEntries = archive.Entries
            .Where(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // For consolidated ZIPs with multiple CSVs, find the one matching the keyword
            var match = csvEntries
                .FirstOrDefault(e => e.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        // Prefer BR national file; fall back to any CSV
        return csvEntries
            .FirstOrDefault(e => e.Name.Contains("BR", StringComparison.OrdinalIgnoreCase))
            ?? csvEntries.FirstOrDefault();
    }

    private static decimal ParseDecimal(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        // TSE uses comma as decimal separator
        s = s.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    private static string NormalizeCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return "";
        cpf = cpf.Replace(".", "").Replace("-", "").Replace("/", "").Trim();
        if (cpf.Length < 11) cpf = cpf.PadLeft(11, '0');
        return cpf;
    }

    /// <summary>Simple CSV split on ';' — handles basic quoted fields.</summary>
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

    // ── Politician lookup helper ──────────────────────────────────────────────

    private sealed class PoliticianLookup(
        Dictionary<string, int> tseKeyToId,
        Dictionary<string, int> cpfToId)
    {
        /// <summary>
        /// Tries to find politician by TSE key ("tse-{sqCandidato}") first, then by CPF.
        /// </summary>
        public int? FindPolitician(string sqCandidato, string cpf)
        {
            // Try ExternalId match (works for non-federal TSE-seeded politicians)
            if (!string.IsNullOrWhiteSpace(sqCandidato))
            {
                var tseKey = $"tse-{sqCandidato}";
                if (tseKeyToId.TryGetValue(tseKey, out var id1))
                    return id1;
            }

            // Fallback: CPF match (works for senators, federal deputies with CPF stored)
            if (!string.IsNullOrWhiteSpace(cpf) && cpfToId.TryGetValue(cpf, out var id2))
                return id2;

            return null;
        }
    }
}
