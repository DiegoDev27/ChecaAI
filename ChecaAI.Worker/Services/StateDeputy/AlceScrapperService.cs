using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

/// <summary>
/// Scrapper for Ceará (ALCE).
/// Uses SSL bypass because the ALCE server has an invalid certificate.
/// Tries /parlamentares and /deputados endpoints to find the deputies list.
/// The response structure will be logged on first run to validate the mapping.
/// </summary>
public class AlceScrapperService : IStateDeputyScrapperService
{
    public string StateCode => "CE";
    public string AssemblyName => "ALCE";

    private const string BaseUrl = "https://www2.al.ce.gov.br/api";
    private static readonly string[] DeputyEndpoints = ["/parlamentares", "/deputados", "/vereadores"];

    private readonly HttpClient _httpClient;
    private readonly ILogger<AlceScrapperService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AlceScrapperService(IHttpClientFactory httpClientFactory, ILogger<AlceScrapperService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("StateDeputyInsecure");
        _logger = logger;
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        foreach (var endpoint in DeputyEndpoints)
        {
            try
            {
                var url = $"{BaseUrl}{endpoint}";
                _logger.LogInformation("Trying ALCE endpoint: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("ALCE endpoint {Url} returned {Status}", url, response.StatusCode);
                    continue;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(jsonContent))
                    continue;

                _logger.LogDebug("ALCE API raw response from {Endpoint} (first 500 chars): {Response}",
                    endpoint, jsonContent.Length > 500 ? jsonContent[..500] : jsonContent);

                var deputies = ParseResponse(jsonContent);
                if (deputies.Count > 0)
                {
                    _logger.LogInformation("Successfully fetched {Count} deputies from ALCE via {Endpoint}",
                        deputies.Count, endpoint);
                    return deputies;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ALCE endpoint {Endpoint} failed, trying next", endpoint);
            }
        }

        _logger.LogError("All ALCE endpoints failed — could not fetch deputies for CE");
        return [];
    }

    public async Task<bool> IsSourceAvailableAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{BaseUrl}/parlamentares", HttpCompletionOption.ResponseHeadersRead);
            // Accept any response — even 404 means the server is reachable
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ALCE source is not available");
            return false;
        }
    }

    private List<StateDeputyData> ParseResponse(string jsonContent)
    {
        try
        {
            if (jsonContent.TrimStart().StartsWith('['))
            {
                var list = JsonSerializer.Deserialize<List<AlceDeputyDto>>(jsonContent, JsonOptions);
                return MapList(list);
            }

            var wrapped = JsonSerializer.Deserialize<AlceWrappedResponse>(jsonContent, JsonOptions);
            var items = wrapped?.Parlamentares ?? wrapped?.Deputados ?? wrapped?.Data ?? wrapped?.Results ?? [];
            return MapList(items);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error for ALCE response — check debug logs for raw response");
            return [];
        }
    }

    private List<StateDeputyData> MapList(List<AlceDeputyDto>? deputies)
    {
        if (deputies == null || deputies.Count == 0)
            return [];

        return deputies
            .Where(d => !string.IsNullOrWhiteSpace(d.Nome ?? d.NomeParlamentar))
            .Select(MapToStateDeputyData)
            .ToList();
    }

    private StateDeputyData MapToStateDeputyData(AlceDeputyDto dto)
    {
        var id = dto.Id?.ToString() ?? dto.IdParlamentar?.ToString();
        var name = (dto.NomeParlamentar ?? dto.Nome ?? string.Empty).Trim();

        return new StateDeputyData
        {
            ExternalId = id ?? name.ToLowerInvariant(),
            FullName = name,
            Party = dto.Partido ?? dto.SiglaPartido,
            StateCode = StateCode,
            Email = dto.Email,
            PhotoUrl = dto.Foto ?? dto.UrlFoto
        };
    }

    private class AlceDeputyDto
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("idParlamentar")]
        public int? IdParlamentar { get; set; }

        [JsonPropertyName("nome")]
        public string? Nome { get; set; }

        [JsonPropertyName("nomeParlamentar")]
        public string? NomeParlamentar { get; set; }

        [JsonPropertyName("partido")]
        public string? Partido { get; set; }

        [JsonPropertyName("siglaPartido")]
        public string? SiglaPartido { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("foto")]
        public string? Foto { get; set; }

        [JsonPropertyName("urlFoto")]
        public string? UrlFoto { get; set; }
    }

    private class AlceWrappedResponse
    {
        [JsonPropertyName("parlamentares")]
        public List<AlceDeputyDto>? Parlamentares { get; set; }

        [JsonPropertyName("deputados")]
        public List<AlceDeputyDto>? Deputados { get; set; }

        [JsonPropertyName("data")]
        public List<AlceDeputyDto>? Data { get; set; }

        [JsonPropertyName("results")]
        public List<AlceDeputyDto>? Results { get; set; }
    }
}
