using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

/// <summary>
/// Scrapper for Espírito Santo (ALES).
/// Uses SSL bypass because the ALES server has an invalid certificate chain.
/// The response structure will be logged on first run to validate the mapping.
/// </summary>
public class AlesScrapperService : IStateDeputyScrapperService
{
    public string StateCode => "ES";
    public string AssemblyName => "ALES";

    private const string ApiUrl = "https://www3.al.es.gov.br/api/publico/parlamentar/";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AlesScrapperService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AlesScrapperService(IHttpClientFactory httpClientFactory, ILogger<AlesScrapperService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("StateDeputyInsecure");
        _logger = logger;
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching deputies from ALES API (SSL bypass)...");

            var jsonContent = await _httpClient.GetStringAsync(ApiUrl);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogWarning("Empty response from ALES API");
                return [];
            }

            _logger.LogDebug("ALES API raw response (first 500 chars): {Response}",
                jsonContent.Length > 500 ? jsonContent[..500] : jsonContent);

            // Try array format first
            if (jsonContent.TrimStart().StartsWith('['))
            {
                var deputies = JsonSerializer.Deserialize<List<AlesDeputyDto>>(jsonContent, JsonOptions);
                return MapList(deputies);
            }

            // Try wrapped object format
            var wrapped = JsonSerializer.Deserialize<AlesWrappedResponse>(jsonContent, JsonOptions);
            var items = wrapped?.Parlamentares ?? wrapped?.Deputados ?? wrapped?.Data ?? [];
            return MapList(items);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching ALES data");
            throw new InvalidOperationException("Failed to fetch data from ALES API", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error for ALES response — check debug logs for raw response");
            throw new InvalidOperationException("Invalid JSON from ALES API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching ALES data");
            throw;
        }
    }

    public async Task<bool> IsSourceAvailableAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(ApiUrl, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ALES source is not available");
            return false;
        }
    }

    private List<StateDeputyData> MapList(List<AlesDeputyDto>? deputies)
    {
        if (deputies == null || deputies.Count == 0)
        {
            _logger.LogWarning("No deputies parsed from ALES API — response structure may differ from expected");
            return [];
        }

        var result = deputies
            .Where(d => !string.IsNullOrWhiteSpace(d.Nome ?? d.NomeParlamentar))
            .Select(MapToStateDeputyData)
            .ToList();

        _logger.LogInformation("Successfully fetched {Count} deputies from ALES", result.Count);
        return result;
    }

    private StateDeputyData MapToStateDeputyData(AlesDeputyDto dto)
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

    // Defensive DTO covering common REST naming patterns in Brazilian state assemblies
    private class AlesDeputyDto
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

    private class AlesWrappedResponse
    {
        [JsonPropertyName("parlamentares")]
        public List<AlesDeputyDto>? Parlamentares { get; set; }

        [JsonPropertyName("deputados")]
        public List<AlesDeputyDto>? Deputados { get; set; }

        [JsonPropertyName("data")]
        public List<AlesDeputyDto>? Data { get; set; }
    }
}
