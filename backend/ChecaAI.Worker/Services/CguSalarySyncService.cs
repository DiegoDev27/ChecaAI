using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ChecaAI.Domain.Entities;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Worker.Services;

/// <summary>
/// Syncs salary records for all active politicians that have a CPF registered.
/// Source: Portal da Transparência CGU — /api-de-dados/servidores/remuneracao
/// Requires a free API key at portaldatransparencia.gov.br/api-de-dados (set CguApiKey in appsettings).
/// Runs once at startup (after 10min delay), then every 24 hours.
/// </summary>
public class CguSalarySyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CguSalarySyncService> _logger;

    private const string CguApiBase = "https://api.portaldatransparencia.gov.br/api-de-dados";
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(150);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public CguSalarySyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CguSalarySyncService> logger)
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
            _logger.LogWarning("[CguSalarySync] CguApiKey not configured — skipping salary sync. Register at portaldatransparencia.gov.br/api-de-dados");
            return;
        }

        _logger.LogInformation("[CguSalarySync] Service started — first run in {Delay}min", StartupDelay.TotalMinutes);
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncSalariesAsync(apiKey, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CguSalarySync] Unexpected error during sync");
            }

            await Task.Delay(SyncInterval, stoppingToken);
        }
    }

    private async Task SyncSalariesAsync(string apiKey, CancellationToken ct)
    {
        // Sync last 12 months
        var months = Enumerable.Range(0, 12)
            .Select(i => DateTime.UtcNow.AddMonths(-i))
            .Select(d => (d.Year, d.Month))
            .ToList();

        // Load politicians with CPF
        List<(int Id, string Cpf, string Name)> politicians;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();
            politicians = await db.Politicians
                .Where(p => p.IsActive && p.Cpf != null &&
                            (p.PoliticalPosition == "Senator" || p.PoliticalPosition == "Federal Deputy"))
                .Select(p => new { p.Id, p.Cpf, p.FullName })
                .AsNoTracking()
                .ToListAsync(ct)
                .ContinueWith(t => t.Result
                    .Select(x => (x.Id, x.Cpf!, x.FullName))
                    .ToList(), ct);
        }

        _logger.LogInformation("[CguSalarySync] Starting salary sync for {Count} politicians ({Months} months each)",
            politicians.Count, months.Count);

        var client = _httpClientFactory.CreateClient("Cgu");

        var totalAdded = 0;

        foreach (var (id, cpf, name) in politicians)
        {
            if (ct.IsCancellationRequested) break;

            foreach (var (year, month) in months)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var records = await FetchSalaryAsync(client, cpf, year, month, ct);
                    if (records.Count > 0)
                    {
                        var added = await PersistSalariesAsync(id, year, month, records, ct);
                        totalAdded += added;
                        if (added > 0)
                            _logger.LogDebug("[CguSalarySync] {Name} ({Year}/{Month:D2}): +{Added} records", name, year, month, added);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[CguSalarySync] Failed for {Name} {Year}/{Month:D2} — skipping", name, year, month);
                }

                await Task.Delay(RateLimitDelay, ct);
            }
        }

        _logger.LogInformation("[CguSalarySync] Done — added {Total} salary records", totalAdded);
    }

    private async Task<List<CguSalaryRecord>> FetchSalaryAsync(
        HttpClient client, string cpf, int year, int month, CancellationToken ct)
    {
        var mesAno = $"{year:D4}{month:D2}";
        var url = $"{CguApiBase}/servidores/remuneracao?cpf={cpf}&mesAno={mesAno}&pagina=1&tamanhoPagina=10";

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[CguSalarySync] HTTP error for CPF {Cpf}", cpf);
            return [];
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogDebug("[CguSalarySync] HTTP {Status} for CPF {Cpf} {MesAno}", response.StatusCode, cpf, mesAno);
            return [];
        }

        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync(ct);

        // Detect AWS WAF CAPTCHA challenge (returned as 200 OK with HTML body)
        if (json.TrimStart().StartsWith('<'))
        {
            _logger.LogWarning("[CguSalarySync] CGU API returned HTML (WAF/CAPTCHA block) — salary data unavailable from this environment. The Portal da Transparência requires browser-authenticated access.");
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<CguSalaryRecord>>(json, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<int> PersistSalariesAsync(
        int politicianId, int year, int month, List<CguSalaryRecord> records, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        // Check if salary for this politician + year + month already exists
        var alreadyExists = await db.PoliticianSalaries
            .AnyAsync(s => s.PoliticianId == politicianId && s.Year == year && s.Month == month, ct);

        if (alreadyExists)
            return 0;

        var added = 0;
        foreach (var r in records)
        {
            db.PoliticianSalaries.Add(new PoliticianSalary
            {
                PoliticianId = politicianId,
                GrossSalary = r.RemuneracaoBasicaBruta,
                NetSalary = r.RendimentoLiquido,
                Allowances = r.OutrasRemuneracoes,
                Month = month,
                Year = year,
                Source = "CGU",
                CreatedAt = DateTime.UtcNow
            });
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        return added;
    }

    // ---- Internal DTOs ----

    private sealed class CguSalaryRecord
    {
        [JsonPropertyName("remuneracaoBasicaBruta")]
        public decimal RemuneracaoBasicaBruta { get; set; }

        [JsonPropertyName("outrasRemuneracoes")]
        public decimal OutrasRemuneracoes { get; set; }

        [JsonPropertyName("rendimentoLiquido")]
        public decimal RendimentoLiquido { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
