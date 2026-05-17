using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

/// <summary>
/// Scrapper for Distrito Federal (CLDF — Câmara Legislativa do DF).
/// API at dadosabertos.cl.df.gov.br is intermittently available (503).
/// Retries up to 3 times and returns empty list on total failure — never throws.
/// </summary>
public class AldfScrapperService : IStateDeputyScrapperService
{
    public string StateCode => "DF";
    public string AssemblyName => "CLDF";

    private const string BaseUrl = "https://dadosabertos.cl.df.gov.br";
    private static readonly string[] Endpoints =
    [
        "/api/deputados-distritais",
        "/api/parlamentares",
        "/api/deputados",
        "/api/vereadores"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AldfScrapperService> _logger;

    public AldfScrapperService(IHttpClientFactory httpClientFactory, ILogger<AldfScrapperService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("StateDeputy");
        _logger = logger;
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        foreach (var endpoint in Endpoints)
        {
            var url = $"{BaseUrl}{endpoint}";
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    _logger.LogInformation("Trying CLDF endpoint {Url} (attempt {Attempt}/3)", url, attempt);

                    var response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogDebug("CLDF endpoint {Url} returned {Status}", url, response.StatusCode);
                        break; // try next endpoint
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(json))
                        break;

                    _logger.LogDebug("CLDF raw response from {Endpoint} (first 500 chars): {Response}",
                        endpoint, json.Length > 500 ? json[..500] : json);

                    var deputies = ParseResponse(json, endpoint);
                    if (deputies.Count > 0)
                    {
                        _logger.LogInformation("Successfully fetched {Count} deputies from CLDF via {Endpoint}",
                            deputies.Count, endpoint);
                        return deputies;
                    }

                    break; // parsed ok but empty — try next endpoint
                }
                catch (HttpRequestException ex) when (attempt < 3)
                {
                    _logger.LogDebug(ex, "CLDF attempt {Attempt} failed for {Url}, retrying in 5s", attempt, url);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CLDF endpoint {Url} failed on attempt {Attempt}", url, attempt);
                    break;
                }
            }
        }

        _logger.LogWarning("All CLDF endpoints failed or returned empty — DF deputies not available right now");
        return [];
    }

    public async Task<bool> IsSourceAvailableAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{BaseUrl}/api/deputados-distritais", HttpCompletionOption.ResponseHeadersRead);
            // 503 means the server exists but is temporarily down — not permanently gone
            return response.IsSuccessStatusCode || (int)response.StatusCode == 503;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CLDF source connectivity check failed");
            return false;
        }
    }

    private List<StateDeputyData> ParseResponse(string json, string endpoint)
    {
        try
        {
            if (json.TrimStart().StartsWith('['))
            {
                var list = JsonSerializer.Deserialize<List<CldfDeputyDto>>(json, JsonOptions);
                return MapList(list);
            }

            var wrapped = JsonSerializer.Deserialize<CldfWrappedResponse>(json, JsonOptions);
            var items = wrapped?.Deputados ?? wrapped?.Parlamentares ?? wrapped?.Data ?? wrapped?.Results ?? [];
            return MapList(items);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error for CLDF {Endpoint} — check debug logs", endpoint);
            return [];
        }
    }

    private List<StateDeputyData> MapList(List<CldfDeputyDto>? list)
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
                    StateCode = StateCode,
                    Email = d.Email,
                    PhotoUrl = d.Foto ?? d.UrlFoto
                };
            })
            .ToList();
    }

    private class CldfDeputyDto
    {
        [JsonPropertyName("id")] public int? Id { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("nomeParlamentar")] public string? NomeParlamentar { get; set; }
        [JsonPropertyName("partido")] public string? Partido { get; set; }
        [JsonPropertyName("siglaPartido")] public string? SiglaPartido { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("foto")] public string? Foto { get; set; }
        [JsonPropertyName("urlFoto")] public string? UrlFoto { get; set; }
    }

    private class CldfWrappedResponse
    {
        [JsonPropertyName("deputados")] public List<CldfDeputyDto>? Deputados { get; set; }
        [JsonPropertyName("parlamentares")] public List<CldfDeputyDto>? Parlamentares { get; set; }
        [JsonPropertyName("data")] public List<CldfDeputyDto>? Data { get; set; }
        [JsonPropertyName("results")] public List<CldfDeputyDto>? Results { get; set; }
    }
}
