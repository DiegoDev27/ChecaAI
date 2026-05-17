using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

/// <summary>
/// Scrapper for Paraná (ALEP — Assembleia Legislativa do Paraná).
/// Uses SSL bypass because the ALEP webservice has an invalid TLS certificate.
/// Tries direct deputy endpoints first; falls back to extracting unique authors from propositions.
/// </summary>
public class AlepScrapperService : IStateDeputyScrapperService
{
    public string StateCode => "PR";
    public string AssemblyName => "ALEP";

    private const string BaseUrl = "http://webservices.assembleia.pr.leg.br";
    private static readonly string[] DirectEndpoints =
    [
        "/api/public/deputado",
        "/api/public/deputados",
        "/api/public/parlamentar",
        "/api/public/parlamentares"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AlepScrapperService> _logger;

    public AlepScrapperService(IHttpClientFactory httpClientFactory, ILogger<AlepScrapperService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("StateDeputyInsecure");
        _logger = logger;
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        // Try direct endpoints first
        foreach (var endpoint in DirectEndpoints)
        {
            try
            {
                var url = $"{BaseUrl}{endpoint}";
                _logger.LogInformation("Trying ALEP endpoint: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("ALEP endpoint {Url} returned {Status}", url, response.StatusCode);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json)) continue;

                _logger.LogDebug("ALEP response from {Endpoint} (first 500 chars): {Json}",
                    endpoint, json.Length > 500 ? json[..500] : json);

                var deputies = ParseDirectResponse(json, endpoint);
                if (deputies.Count > 0)
                {
                    _logger.LogInformation("Successfully fetched {Count} deputies from ALEP via {Endpoint}",
                        deputies.Count, endpoint);
                    return deputies;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ALEP endpoint {Endpoint} failed, trying next", endpoint);
            }
        }

        // Fallback: extract unique deputy authors from propositions
        _logger.LogInformation("Direct endpoints failed — trying ALEP propositions workaround...");
        return await FetchFromPropositionsAsync();
    }

    public async Task<bool> IsSourceAvailableAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{BaseUrl}/api/public", HttpCompletionOption.ResponseHeadersRead);
            return true; // any response means the server is reachable
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ALEP source is not available");
            return false;
        }
    }

    private async Task<List<StateDeputyData>> FetchFromPropositionsAsync()
    {
        var deputiesByName = new Dictionary<string, StateDeputyData>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var page = 1;
            var hasMore = true;

            while (hasMore && page <= 50) // safety cap: 50 pages max
            {
                var url = $"{BaseUrl}/api/public/proposicao?page={page}";
                _logger.LogDebug("Fetching ALEP propositions page {Page}", page);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) break;

                var json = await response.Content.ReadAsStringAsync();
                var pageResponse = JsonSerializer.Deserialize<AlepPropositionPageResponse>(json, JsonOptions);

                if (pageResponse?.Results == null || pageResponse.Results.Count == 0)
                    break;

                foreach (var prop in pageResponse.Results)
                {
                    var name = (prop.Autor?.Nome ?? prop.AutorNome ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(name) || deputiesByName.ContainsKey(name))
                        continue;

                    deputiesByName[name] = new StateDeputyData
                    {
                        ExternalId = prop.Autor?.Id?.ToString() ?? name.ToLowerInvariant(),
                        FullName = name,
                        Party = prop.Autor?.Partido,
                        StateCode = StateCode
                    };
                }

                hasMore = !string.IsNullOrEmpty(pageResponse.Next);
                page++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ALEP propositions fallback failed");
            return [];
        }

        var result = deputiesByName.Values.ToList();
        _logger.LogInformation("Extracted {Count} unique deputies from ALEP propositions", result.Count);
        return result;
    }

    private List<StateDeputyData> ParseDirectResponse(string json, string endpoint)
    {
        try
        {
            if (json.TrimStart().StartsWith('['))
            {
                var list = JsonSerializer.Deserialize<List<AlepDeputyDto>>(json, JsonOptions);
                return MapList(list);
            }

            var wrapped = JsonSerializer.Deserialize<AlepWrappedResponse>(json, JsonOptions);
            var items = wrapped?.Deputados ?? wrapped?.Parlamentares ?? wrapped?.Data ?? wrapped?.Results ?? [];
            return MapList(items);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error for ALEP {Endpoint}", endpoint);
            return [];
        }
    }

    private List<StateDeputyData> MapList(List<AlepDeputyDto>? list)
    {
        if (list == null || list.Count == 0) return [];

        return list
            .Where(d => !string.IsNullOrWhiteSpace(d.Nome ?? d.NomeParlamentar))
            .Select(d =>
            {
                var name = (d.NomeParlamentar ?? d.Nome ?? string.Empty).Trim();
                return new StateDeputyData
                {
                    ExternalId = d.Id?.ToString() ?? name.ToLowerInvariant(),
                    FullName = name,
                    Party = d.Partido ?? d.SiglaPartido,
                    StateCode = StateCode
                };
            })
            .ToList();
    }

    private class AlepDeputyDto
    {
        [JsonPropertyName("id")] public int? Id { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("nomeParlamentar")] public string? NomeParlamentar { get; set; }
        [JsonPropertyName("partido")] public string? Partido { get; set; }
        [JsonPropertyName("siglaPartido")] public string? SiglaPartido { get; set; }
    }

    private class AlepWrappedResponse
    {
        [JsonPropertyName("deputados")] public List<AlepDeputyDto>? Deputados { get; set; }
        [JsonPropertyName("parlamentares")] public List<AlepDeputyDto>? Parlamentares { get; set; }
        [JsonPropertyName("data")] public List<AlepDeputyDto>? Data { get; set; }
        [JsonPropertyName("results")] public List<AlepDeputyDto>? Results { get; set; }
    }

    private class AlepPropositionPageResponse
    {
        [JsonPropertyName("next")] public string? Next { get; set; }
        [JsonPropertyName("results")] public List<AlepPropositionDto>? Results { get; set; }
    }

    private class AlepPropositionDto
    {
        [JsonPropertyName("autor")] public AlepAutorDto? Autor { get; set; }
        [JsonPropertyName("autorNome")] public string? AutorNome { get; set; }
    }

    private class AlepAutorDto
    {
        [JsonPropertyName("id")] public int? Id { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("partido")] public string? Partido { get; set; }
    }
}
