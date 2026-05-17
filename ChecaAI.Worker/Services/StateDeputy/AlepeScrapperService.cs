using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

public class AlepeScrapperService : IStateDeputyScrapperService
{
    public string StateCode => "PE";
    public string AssemblyName => "ALEPE";

    private const string ApiUrl = "https://dadosabertos.alepe.pe.gov.br/api/v1/parlamentares/";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AlepeScrapperService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AlepeScrapperService(IHttpClientFactory httpClientFactory, ILogger<AlepeScrapperService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("StateDeputy");
        _logger = logger;
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching deputies from ALEPE API...");

            var jsonContent = await _httpClient.GetStringAsync(ApiUrl);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogWarning("Empty response from ALEPE API");
                return [];
            }

            var deputies = JsonSerializer.Deserialize<List<AlepeDeputyDto>>(jsonContent, JsonOptions);

            if (deputies == null || deputies.Count == 0)
            {
                _logger.LogWarning("No deputies parsed from ALEPE API");
                return [];
            }

            var result = deputies
                .Where(d => !string.IsNullOrWhiteSpace(d.NomeParlamentar))
                .Select(MapToStateDeputyData)
                .ToList();

            _logger.LogInformation("Successfully fetched {Count} deputies from ALEPE", result.Count);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching ALEPE data");
            throw new InvalidOperationException("Failed to fetch data from ALEPE API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching ALEPE data");
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
            _logger.LogWarning(ex, "ALEPE source is not available");
            return false;
        }
    }

    private StateDeputyData MapToStateDeputyData(AlepeDeputyDto dto)
    {
        var name = dto.NomeParlamentar!.Trim();
        return new StateDeputyData
        {
            ExternalId = name.ToLowerInvariant(),
            FullName = name,
            Party = dto.Partido,
            StateCode = StateCode
        };
    }

    private class AlepeDeputyDto
    {
        [JsonPropertyName("nomeParlamentar")]
        public string? NomeParlamentar { get; set; }

        [JsonPropertyName("partido")]
        public string? Partido { get; set; }
    }
}
