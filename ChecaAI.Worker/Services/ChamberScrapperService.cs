using System.Text.Json;
using ChecaAI.Infrastructure.DTOs;
using ChecaAI.Worker.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChecaAI.Worker.Services;

public interface IChamberScrapperService
{
    Task<List<DeputyDto>> FetchFederalDeputiesAsync();
    Task<bool> IsApiAvailableAsync();
}

public class ChamberScrapperService : IChamberScrapperService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChamberScrapperService> _logger;
    private readonly ChamberApiOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ChamberScrapperService(
        HttpClient httpClient,
        ILogger<ChamberScrapperService> logger,
        IOptions<ChamberApiOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _httpClient.Timeout = _options.RequestTimeout;
    }

    public async Task<List<DeputyDto>> FetchFederalDeputiesAsync()
    {
        var allDeputies = new List<DeputyDto>();
        var page = 1;

        try
        {
            _logger.LogInformation("Starting Chamber API data fetch (paginated)...");

            while (true)
            {
                var url = $"{_options.BaseUrl}/deputados?itens={_options.PageSize}&pagina={page}";
                _logger.LogDebug("Fetching page {Page}: {Url}", page, url);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("Empty response on page {Page}", page);
                    break;
                }

                var pageResponse = JsonSerializer.Deserialize<ChamberOfDeputiesResponse<DeputyDto>>(jsonContent, JsonOptions);

                if (pageResponse?.Data == null || pageResponse.Data.Count == 0)
                    break;

                allDeputies.AddRange(pageResponse.Data);
                _logger.LogDebug("Page {Page}: fetched {Count} deputies (total so far: {Total})",
                    page, pageResponse.Data.Count, allDeputies.Count);

                var hasNextPage = pageResponse.Links.Any(l => l.Rel == "next");
                if (!hasNextPage)
                    break;

                page++;
            }

            _logger.LogInformation("Successfully fetched {Total} federal deputies across {Pages} page(s)",
                allDeputies.Count, page);

            return allDeputies;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching Chamber data");
            throw new InvalidOperationException("Failed to fetch data from Chamber API", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout while fetching Chamber data");
            throw new InvalidOperationException("Timeout while fetching Chamber API data", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching Chamber data");
            throw;
        }
    }

    public async Task<bool> IsApiAvailableAsync()
    {
        try
        {
            var url = $"{_options.BaseUrl}/deputados?itens=1&pagina=1";
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chamber API is not available");
            return false;
        }
    }
}
