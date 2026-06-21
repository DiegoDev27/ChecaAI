using System.Text.Json;
using System.Text.Json.Serialization;
using ChecaAI.Worker.Configuration;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

public class SaplScrapperService : IStateDeputyScrapperService
{
    public string StateCode => _config.StateCode;
    public string AssemblyName => _config.AssemblyName;

    private readonly SaplAssemblyConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SaplScrapperService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SaplScrapperService(
        SaplAssemblyConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<SaplScrapperService> logger)
    {
        _config = config;
        _httpClient = httpClientFactory.CreateClient("StateDeputy");
        _logger = logger;
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        var allDeputies = new List<StateDeputyData>();
        var nextUrl = $"{_config.BaseUrl}/api/parlamentares/parlamentar/";

        try
        {
            _logger.LogInformation("Fetching deputies from SAPL ({State} - {Assembly})...",
                _config.StateCode, _config.AssemblyName);

            // Fetch sequentially to avoid 429 on rate-limited SAPL instances
            var partyMap = await FetchPartyMapAsync();
            var activeMandateIds = await FetchActiveMandateSetAsync();

            while (!string.IsNullOrEmpty(nextUrl))
            {
                await Task.Delay(500);
                var response = await _httpClient.GetAsync(nextUrl);
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(jsonContent))
                    break;

                var pageResponse = JsonSerializer.Deserialize<SaplPageResponse>(jsonContent, JsonOptions);

                if (pageResponse?.Results == null || pageResponse.Results.Count == 0)
                    break;

                var activeDeputies = pageResponse.Results
                    .Where(d => d.Ativo)
                    // Cross-check with mandate data: exclude ex-deputies who became prefeitos etc.
                    // Fallback to ativo-only if mandato fetch failed (activeMandateIds is empty)
                    .Where(d => activeMandateIds.Count == 0 || activeMandateIds.Contains(d.Id))
                    .Select(d => MapToStateDeputyData(d, partyMap))
                    .ToList();

                allDeputies.AddRange(activeDeputies);

                nextUrl = pageResponse.Pagination?.Links?.Next ?? string.Empty;
            }

            _logger.LogInformation("Successfully fetched {Count} active deputies from SAPL ({State} - {Assembly})",
                allDeputies.Count, _config.StateCode, _config.AssemblyName);

            return allDeputies;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching SAPL data for {Assembly}", _config.AssemblyName);
            throw new InvalidOperationException($"Failed to fetch data from SAPL API ({_config.AssemblyName})", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for SAPL response from {Assembly}", _config.AssemblyName);
            throw new InvalidOperationException($"Invalid JSON from SAPL API ({_config.AssemblyName})", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching SAPL data for {Assembly}", _config.AssemblyName);
            throw;
        }
    }

    public async Task<bool> IsSourceAvailableAsync()
    {
        try
        {
            var url = $"{_config.BaseUrl}/api/parlamentares/parlamentar/?page=1";
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SAPL source not available for {Assembly}", _config.AssemblyName);
            return false;
        }
    }

    /// <summary>
    /// Fetches all current party affiliations from /api/parlamentares/filiacao/ and returns a
    /// dictionary mapping parlamentar ID → party sigla. Entries with data_desfiliacao set are
    /// excluded (no longer affiliated). Fails gracefully — returns an empty dict on any error.
    /// </summary>
    private async Task<Dictionary<int, string>> FetchPartyMapAsync()
    {
        var partyMap = new Dictionary<int, string>();
        var nextUrl = $"{_config.BaseUrl}/api/parlamentares/filiacao/";

        try
        {
            while (!string.IsNullOrEmpty(nextUrl))
            {
                await Task.Delay(500);
                var response = await _httpClient.GetAsync(nextUrl);
                if (!response.IsSuccessStatusCode)
                    break;

                var json = await response.Content.ReadAsStringAsync();
                var page = JsonSerializer.Deserialize<SaplFiliacaoPageResponse>(json, JsonOptions);

                if (page?.Results == null || page.Results.Count == 0)
                    break;

                foreach (var f in page.Results)
                {
                    // Only current affiliations (no deaffiliation date)
                    if (f.DataDesfiliacao == null)
                    {
                        var sigla = ExtractSigla(f.Str);
                        if (sigla != null)
                            partyMap[f.Parlamentar] = sigla;
                    }
                }

                nextUrl = page.Pagination?.Links?.Next ?? string.Empty;
            }

            _logger.LogInformation("Fetched {Count} party affiliations for {Assembly}",
                partyMap.Count, _config.AssemblyName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not fetch party affiliations from SAPL ({Assembly}) — deputies will have no party",
                _config.AssemblyName);
        }

        return partyMap;
    }

    /// <summary>
    /// Returns the set of parlamentar IDs that have a current, uninterrupted mandate.
    /// Excludes anyone whose mandate ended early (tipo_causa_fim_mandato != null), e.g.
    /// deputies who left to become prefeitos. Returns empty set on failure (caller falls
    /// back to ativo-only filter).
    /// </summary>
    private async Task<HashSet<int>> FetchActiveMandateSetAsync()
    {
        var activeIds = new HashSet<int>();
        var today = DateTime.UtcNow.Date;
        var nextUrl = $"{_config.BaseUrl}/api/parlamentares/mandato/";

        try
        {
            while (!string.IsNullOrEmpty(nextUrl))
            {
                await Task.Delay(500);
                var response = await _httpClient.GetAsync(nextUrl);
                if (!response.IsSuccessStatusCode) break;

                var json = await response.Content.ReadAsStringAsync();
                var page = JsonSerializer.Deserialize<SaplMandatoPageResponse>(json, JsonOptions);
                if (page?.Results == null || page.Results.Count == 0) break;

                foreach (var m in page.Results)
                {
                    if (m.TipoCausaFimMandato == null
                        && m.DataFimMandato.HasValue
                        && m.DataFimMandato.Value.Date >= today)
                    {
                        activeIds.Add(m.Parlamentar);
                    }
                }

                nextUrl = page.Pagination?.Links?.Next ?? string.Empty;
            }

            _logger.LogInformation("Found {Count} parlamentares with active mandate for {Assembly}",
                activeIds.Count, _config.AssemblyName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not fetch mandate data for {Assembly} — falling back to ativo filter only",
                _config.AssemblyName);
        }

        return activeIds;
    }

    private StateDeputyData MapToStateDeputyData(SaplDeputyDto dto, Dictionary<int, string> partyMap)
    {
        partyMap.TryGetValue(dto.Id, out var party);
        return new StateDeputyData
        {
            ExternalId = dto.Id.ToString(),
            FullName = dto.NomeCompleto ?? dto.NomeParlamentar ?? string.Empty,
            Party = party,
            StateCode = _config.StateCode,
            Email = dto.Email,
            PhotoUrl = dto.Fotografia,
            ParlamentaryPageUrl = dto.EnderecoWeb
        };
    }

    // SAPL response DTOs
    private class SaplPageResponse
    {
        [JsonPropertyName("pagination")]
        public SaplPagination? Pagination { get; set; }

        [JsonPropertyName("results")]
        public List<SaplDeputyDto> Results { get; set; } = [];
    }

    private class SaplFiliacaoPageResponse
    {
        [JsonPropertyName("pagination")]
        public SaplPagination? Pagination { get; set; }

        [JsonPropertyName("results")]
        public List<SaplFiliacaoDto> Results { get; set; } = [];
    }

    private class SaplPagination
    {
        [JsonPropertyName("links")]
        public SaplPaginationLinks? Links { get; set; }

        [JsonPropertyName("total_entries")]
        public int TotalEntries { get; set; }

        [JsonPropertyName("total_pages")]
        public int TotalPages { get; set; }
    }

    private class SaplPaginationLinks
    {
        [JsonPropertyName("next")]
        public string? Next { get; set; }

        [JsonPropertyName("previous")]
        public string? Previous { get; set; }
    }

    private class SaplDeputyDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nome_completo")]
        public string? NomeCompleto { get; set; }

        [JsonPropertyName("nome_parlamentar")]
        public string? NomeParlamentar { get; set; }

        [JsonPropertyName("sexo")]
        public string? Sexo { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("fotografia")]
        public string? Fotografia { get; set; }

        [JsonPropertyName("endereco_web")]
        public string? EnderecoWeb { get; set; }

        [JsonPropertyName("ativo")]
        public bool Ativo { get; set; }
    }

    private class SaplFiliacaoDto
    {
        [JsonPropertyName("parlamentar")]
        public int Parlamentar { get; set; }

        [JsonPropertyName("data_desfiliacao")]
        public string? DataDesfiliacao { get; set; }

        // Format: "NOME - SIGLA - NOME COMPLETO DO PARTIDO"
        [JsonPropertyName("__str__")]
        public string? Str { get; set; }
    }

    private static string? ExtractSigla(string? str)
    {
        if (str == null) return null;
        var parts = str.Split(" - ");
        return parts.Length >= 2 ? parts[1].Trim() : null;
    }

    private class SaplMandatoPageResponse
    {
        [JsonPropertyName("pagination")]
        public SaplPagination? Pagination { get; set; }

        [JsonPropertyName("results")]
        public List<SaplMandatoDto> Results { get; set; } = [];
    }

    private class SaplMandatoDto
    {
        [JsonPropertyName("parlamentar")]
        public int Parlamentar { get; set; }

        [JsonPropertyName("data_fim_mandato")]
        public DateTime? DataFimMandato { get; set; }

        // Typed as object? because when non-null it can be an int FK or a string depending on SAPL version.
        // We only need to know whether it is null or not.
        [JsonPropertyName("tipo_causa_fim_mandato")]
        public object? TipoCausaFimMandato { get; set; }
    }
}
