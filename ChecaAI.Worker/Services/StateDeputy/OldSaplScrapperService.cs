using System.Text.Json;
using System.Text.Json.Serialization;
using ChecaAI.Worker.Configuration;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

/// <summary>
/// Scrapper for older SAPL installations (e.g., MA — Maranhão) that do not expose the modern
/// paginated REST API at /api/parlamentares/parlamentar/ but may expose a legacy flat JSON endpoint.
/// Tries several legacy endpoint patterns and logs response structure on first run.
/// </summary>
public class OldSaplScrapperService : IStateDeputyScrapperService
{
    public string StateCode => _config.StateCode;
    public string AssemblyName => _config.AssemblyName;

    private static readonly string[] EndpointPatterns =
    [
        "/sapl/parlamentar/parlamentar.json",
        "/sapl/parlamentar/parlamentar/?format=json",
        "/sapl/api/parlamentar/?format=json",
        "/api/parlamentares/",
        "/api/parlamentar/"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SaplAssemblyConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OldSaplScrapperService> _logger;

    public OldSaplScrapperService(
        SaplAssemblyConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<OldSaplScrapperService> logger)
    {
        _config = config;
        _httpClient = httpClientFactory.CreateClient("StateDeputy");
        _logger = logger;
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        var partyMap = await FetchPartyMapAsync();

        foreach (var pattern in EndpointPatterns)
        {
            var url = $"{_config.BaseUrl}{pattern}";
            try
            {
                _logger.LogInformation("Trying old SAPL endpoint ({State}): {Url}", _config.StateCode, url);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Old SAPL {Url} returned {Status}", url, response.StatusCode);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json)) continue;

                _logger.LogDebug("Old SAPL ({Assembly}) raw response from {Pattern} (first 500 chars): {Json}",
                    _config.AssemblyName, pattern, json.Length > 500 ? json[..500] : json);

                var deputies = ParseResponse(json, pattern, partyMap);
                if (deputies.Count > 0)
                {
                    _logger.LogInformation("Fetched {Count} deputies from old SAPL ({Assembly}) via {Pattern}",
                        deputies.Count, _config.AssemblyName, pattern);
                    return deputies;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Old SAPL endpoint {Url} failed, trying next", url);
            }
        }

        _logger.LogWarning("All old SAPL endpoints failed for {Assembly} ({State})",
            _config.AssemblyName, _config.StateCode);
        return [];
    }

    public async Task<bool> IsSourceAvailableAsync()
    {
        foreach (var pattern in EndpointPatterns)
        {
            try
            {
                var url = $"{_config.BaseUrl}{pattern}";
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch
            {
                // try next
            }
        }

        _logger.LogWarning("Old SAPL source not available for {Assembly}", _config.AssemblyName);
        return false;
    }

    /// <summary>
    /// Attempts to fetch current party affiliations from the SAPL filiacao endpoint.
    /// Returns an empty dict if the endpoint is unavailable (old SAPL versions may not have it).
    /// </summary>
    private async Task<Dictionary<int, string>> FetchPartyMapAsync()
    {
        var partyMap = new Dictionary<int, string>();
        var url = $"{_config.BaseUrl}/api/parlamentares/filiacao/";

        try
        {
            var nextUrl = url;
            while (!string.IsNullOrEmpty(nextUrl))
            {
                var response = await _httpClient.GetAsync(nextUrl);
                if (!response.IsSuccessStatusCode) break;

                var json = await response.Content.ReadAsStringAsync();
                var page = JsonSerializer.Deserialize<OldSaplFiliacaoPageResponse>(json, JsonOptions);

                if (page?.Results == null || page.Results.Count == 0) break;

                foreach (var f in page.Results)
                {
                    if (f.DataDesfiliacao == null)
                    {
                        var sigla = ExtractSigla(f.Str);
                        if (sigla != null)
                            partyMap[f.Parlamentar] = sigla;
                    }
                }

                nextUrl = page.Next ?? string.Empty;
            }

            if (partyMap.Count > 0)
                _logger.LogInformation("Fetched {Count} party affiliations for {Assembly}", partyMap.Count, _config.AssemblyName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Party affiliations not available for old SAPL ({Assembly})", _config.AssemblyName);
        }

        return partyMap;
    }

    private List<StateDeputyData> ParseResponse(string json, string pattern, Dictionary<int, string> partyMap)
    {
        try
        {
            // Flat array
            if (json.TrimStart().StartsWith('['))
            {
                var list = JsonSerializer.Deserialize<List<OldSaplDeputyDto>>(json, JsonOptions);
                return MapList(list, partyMap);
            }

            // Wrapped object — try common wrappers
            var wrapped = JsonSerializer.Deserialize<OldSaplWrappedResponse>(json, JsonOptions);
            var items = wrapped?.Parlamentares
                        ?? wrapped?.Deputados
                        ?? wrapped?.Objects
                        ?? wrapped?.Data
                        ?? wrapped?.Results
                        ?? [];
            return MapList(items, partyMap);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error for old SAPL ({Assembly}) {Pattern}", _config.AssemblyName, pattern);
            return [];
        }
    }

    private List<StateDeputyData> MapList(List<OldSaplDeputyDto>? list, Dictionary<int, string> partyMap)
    {
        if (list == null || list.Count == 0) return [];

        return list
            .Where(d => d.Ativo != false) // include if ativo is null (unknown) or true
            .Where(d => !string.IsNullOrWhiteSpace(d.NomeParlamentar ?? d.NomeCompleto ?? d.Nome))
            .Select(d =>
            {
                var name = (d.NomeParlamentar ?? d.NomeCompleto ?? d.Nome ?? string.Empty).Trim();
                var id = d.Id ?? d.Pk;
                id?.ToString();
                partyMap.TryGetValue(id ?? 0, out var party);
                return new StateDeputyData
                {
                    ExternalId = id?.ToString() ?? name.ToLowerInvariant(),
                    FullName = name,
                    Party = party,
                    StateCode = _config.StateCode,
                    Email = d.Email,
                    PhotoUrl = d.Fotografia ?? d.Foto
                };
            })
            .ToList();
    }

    private class OldSaplDeputyDto
    {
        [JsonPropertyName("id")] public int? Id { get; set; }
        [JsonPropertyName("pk")] public int? Pk { get; set; }
        [JsonPropertyName("nome_completo")] public string? NomeCompleto { get; set; }
        [JsonPropertyName("nome_parlamentar")] public string? NomeParlamentar { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("ativo")] public bool? Ativo { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("fotografia")] public string? Fotografia { get; set; }
        [JsonPropertyName("foto")] public string? Foto { get; set; }
    }

    private class OldSaplWrappedResponse
    {
        [JsonPropertyName("parlamentares")] public List<OldSaplDeputyDto>? Parlamentares { get; set; }
        [JsonPropertyName("deputados")] public List<OldSaplDeputyDto>? Deputados { get; set; }
        [JsonPropertyName("objects")] public List<OldSaplDeputyDto>? Objects { get; set; }
        [JsonPropertyName("data")] public List<OldSaplDeputyDto>? Data { get; set; }
        [JsonPropertyName("results")] public List<OldSaplDeputyDto>? Results { get; set; }
    }

    private class OldSaplFiliacaoPageResponse
    {
        [JsonPropertyName("next")] public string? Next { get; set; }
        [JsonPropertyName("results")] public List<OldSaplFiliacaoDto>? Results { get; set; }
    }

    private class OldSaplFiliacaoDto
    {
        [JsonPropertyName("parlamentar")] public int Parlamentar { get; set; }
        [JsonPropertyName("data_desfiliacao")] public string? DataDesfiliacao { get; set; }
        // Format: "NOME - SIGLA - NOME COMPLETO DO PARTIDO"
        [JsonPropertyName("__str__")] public string? Str { get; set; }
    }

    private static string? ExtractSigla(string? str)
    {
        if (str == null) return null;
        var parts = str.Split(" - ");
        return parts.Length >= 2 ? parts[1].Trim() : null;
    }
}
