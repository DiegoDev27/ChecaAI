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
/// Syncs political parties from the Câmara dos Deputados API (/partidos).
/// Runs once at startup, then every 24h.
/// </summary>
public class PartyDataSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PartyDataSyncService> _logger;

    private const string CamaraBaseUrl = "https://dadosabertos.camara.leg.br/api/v2";
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);

    public PartyDataSyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<PartyDataSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[PartySync] Service started — syncing every {Interval}h", SyncInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncPartiesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PartySync] Unexpected error during sync");
            }

            await Task.Delay(SyncInterval, stoppingToken);
        }
    }

    private async Task SyncPartiesAsync(CancellationToken ct)
    {
        _logger.LogInformation("[PartySync] Fetching parties from Câmara API...");

        var client = _httpClientFactory.CreateClient("StateDeputy");
        var parties = new List<CamaraPartyDto>();
        var page = 1;

        while (true)
        {
            var url = $"{CamaraBaseUrl}/partidos?itens=100&pagina={page}&ordem=ASC&ordenarPor=sigla";
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(url, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PartySync] Failed to fetch page {Page}", page);
                break;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[PartySync] HTTP {Status} on page {Page}", response.StatusCode, page);
                break;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<CamaraApiResponse<CamaraPartyDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Data == null || result.Data.Count == 0)
                break;

            parties.AddRange(result.Data);

            // Check for next page via Links
            var hasNext = result.Links?.Any(l => l.Rel == "next") ?? false;
            if (!hasNext) break;
            page++;
        }

        _logger.LogInformation("[PartySync] Fetched {Count} parties from Câmara", parties.Count);

        if (parties.Count == 0)
        {
            _logger.LogWarning("[PartySync] No parties returned — skipping");
            return;
        }

        // Fetch detail for each party to get the full name and number
        var detailedParties = new List<CamaraPartyDetailDto>();
        foreach (var p in parties)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var detailUrl = $"{CamaraBaseUrl}/partidos/{p.Id}";
                var detailResponse = await client.GetAsync(detailUrl, ct);
                if (!detailResponse.IsSuccessStatusCode) continue;

                var detailJson = await detailResponse.Content.ReadAsStringAsync(ct);
                var detailResult = JsonSerializer.Deserialize<CamaraDetailResponse<CamaraPartyDetailDto>>(detailJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (detailResult?.Data != null)
                    detailedParties.Add(detailResult.Data);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[PartySync] Could not fetch detail for party {Acronym}", p.Sigla);
            }
        }

        await PersistPartiesAsync(detailedParties, ct);
    }

    private async Task PersistPartiesAsync(List<CamaraPartyDetailDto> dtos, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        var existingByAcronym = await db.Parties
            .ToDictionaryAsync(p => p.Acronym, StringComparer.OrdinalIgnoreCase, ct);

        var added = 0;
        var updated = 0;

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.Sigla)) continue;

            var president = dto.Lider?.Nome;

            if (existingByAcronym.TryGetValue(dto.Sigla, out var existing))
            {
                // Update
                existing.FullName = dto.Nome ?? existing.FullName;
                existing.Number = dto.NumeroCandidatura;
                existing.ExternalId = dto.Id?.ToString();
                if (!string.IsNullOrWhiteSpace(president))
                    existing.President = president.Length > 200 ? president[..200] : president;
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
            else
            {
                db.Parties.Add(new Party
                {
                    Acronym = dto.Sigla.Length > 20 ? dto.Sigla[..20] : dto.Sigla,
                    FullName = (dto.Nome ?? dto.Sigla).Length > 200 ? (dto.Nome ?? dto.Sigla)[..200] : (dto.Nome ?? dto.Sigla),
                    Number = dto.NumeroCandidatura,
                    ExternalId = dto.Id?.ToString(),
                    President = president?.Length > 200 ? president[..200] : president,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                added++;
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[PartySync] Done — added {Added}, updated {Updated} parties", added, updated);

        // Link politicians to their party records by acronym
        await LinkPoliticiansToPartiesAsync(ct);
    }

    /// <summary>
    /// Sets Politician.PartyId for politicians whose Party string matches an existing Party record.
    /// Idempotent — only updates politicians where PartyId is currently null.
    /// </summary>
    private async Task LinkPoliticiansToPartiesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChecaAIDbContext>();

        var partyMap = await db.Parties
            .Select(p => new { p.Id, p.Acronym })
            .AsNoTracking()
            .ToListAsync(ct)
            .ContinueWith(t => t.Result.ToDictionary(
                p => p.Acronym.ToUpperInvariant(),
                p => p.Id,
                StringComparer.OrdinalIgnoreCase), ct);

        var politicians = await db.Politicians
            .Where(p => p.PartyId == null && p.Party != null)
            .ToListAsync(ct);

        var linked = 0;
        foreach (var pol in politicians)
        {
            if (pol.Party == null) continue;
            var key = pol.Party.Trim().ToUpperInvariant();
            if (!partyMap.TryGetValue(key, out var partyId)) continue;
            pol.PartyId = partyId;
            linked++;
        }

        if (linked > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("[PartySync] Linked {Count} politicians to party records", linked);
        }
    }

    // ---- DTOs ----

    private sealed class CamaraApiResponse<T>
    {
        [JsonPropertyName("dados")]
        public List<T> Data { get; set; } = new();
        [JsonPropertyName("links")]
        public List<CamaraLink>? Links { get; set; }
    }

    private sealed class CamaraDetailResponse<T>
    {
        [JsonPropertyName("dados")]
        public T? Data { get; set; }
    }

    private sealed class CamaraLink
    {
        [JsonPropertyName("rel")]
        public string Rel { get; set; } = string.Empty;
        [JsonPropertyName("href")]
        public string Href { get; set; } = string.Empty;
    }

    private sealed class CamaraPartyDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("sigla")]
        public string Sigla { get; set; } = string.Empty;
        [JsonPropertyName("nome")]
        public string? Nome { get; set; }
    }

    private sealed class CamaraPartyDetailDto
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }
        [JsonPropertyName("sigla")]
        public string Sigla { get; set; } = string.Empty;
        [JsonPropertyName("nome")]
        public string? Nome { get; set; }
        [JsonPropertyName("numeroCandidatura")]
        public int? NumeroCandidatura { get; set; }
        [JsonPropertyName("lider")]
        public CamaraPartyLeader? Lider { get; set; }
    }

    private sealed class CamaraPartyLeader
    {
        [JsonPropertyName("nome")]
        public string? Nome { get; set; }
    }
}
