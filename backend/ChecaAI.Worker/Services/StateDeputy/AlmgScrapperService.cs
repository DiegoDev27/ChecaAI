using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

public class AlmgScrapperService : IStateDeputyScrapperService
{
    public string StateCode => "MG";
    public string AssemblyName => "ALMG";

    private const string BaseUrl = "https://dadosabertos.almg.gov.br";
    private const string PartiesEndpoint = "/ws/representacao_partidaria/partidos?formato=json";
    private const string DeputiesEndpointTemplate = "/ws/representacao_partidaria/partidos/{0}/deputados?formato=json";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AlmgScrapperService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AlmgScrapperService(IHttpClientFactory httpClientFactory, ILogger<AlmgScrapperService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("StateDeputy");
        _logger = logger;
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching parties from ALMG...");

            var partiesJson = await _httpClient.GetStringAsync($"{BaseUrl}{PartiesEndpoint}");
            var partiesResponse = JsonSerializer.Deserialize<AlmgListResponse<AlmgPartidoDto>>(partiesJson, JsonOptions);
            var parties = partiesResponse?.List ?? [];

            if (parties.Count == 0)
            {
                _logger.LogWarning("No parties returned from ALMG");
                return [];
            }

            _logger.LogInformation("Fetching deputies for {Count} parties from ALMG...", parties.Count);

            var deputiesById = new Dictionary<int, StateDeputyData>();

            foreach (var party in parties)
            {
                try
                {
                    await Task.Delay(300); // ALMG rate limit
                    var endpoint = string.Format(DeputiesEndpointTemplate, party.Id);
                    var deputiesJson = await _httpClient.GetStringAsync($"{BaseUrl}{endpoint}");
                    var deputiesResponse = JsonSerializer.Deserialize<AlmgListResponse<AlmgDeputyDto>>(deputiesJson, JsonOptions);
                    var deputies = deputiesResponse?.List ?? [];

                    foreach (var deputy in deputies)
                    {
                        if (!deputiesById.ContainsKey(deputy.Id))
                            deputiesById[deputy.Id] = MapToStateDeputyData(deputy);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch deputies for ALMG party {PartyId} ({Sigla}), skipping",
                        party.Id, party.Sigla);
                }
            }

            var result = deputiesById.Values.ToList();
            _logger.LogInformation("Successfully fetched {Count} unique deputies from ALMG", result.Count);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching ALMG data");
            throw new InvalidOperationException("Failed to fetch data from ALMG API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching ALMG data");
            throw;
        }
    }

    public async Task<bool> IsSourceAvailableAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{BaseUrl}{PartiesEndpoint}", HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ALMG source is not available");
            return false;
        }
    }

    private StateDeputyData MapToStateDeputyData(AlmgDeputyDto dto)
    {
        return new StateDeputyData
        {
            ExternalId = dto.Id.ToString(),
            FullName = dto.Nome ?? string.Empty,
            Party = dto.Partido,
            StateCode = StateCode
        };
    }

    private class AlmgListResponse<T>
    {
        [JsonPropertyName("list")]
        public List<T> List { get; set; } = [];
    }

    private class AlmgPartidoDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("sigla")]
        public string? Sigla { get; set; }
    }

    private class AlmgDeputyDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nome")]
        public string? Nome { get; set; }

        [JsonPropertyName("partido")]
        public string? Partido { get; set; }
    }
}
