using System.Text.Json;
using ChecaAI.Worker.Models.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services;

public interface ISenateScrapperService
{
    Task<SenateApiResponse?> FetchSenatorsDataAsync();
    Task<bool> IsApiAvailableAsync();
}

public class SenateScrapperService : ISenateScrapperService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SenateScrapperService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _senateBaseUrl;

    public SenateScrapperService(
        HttpClient httpClient, 
        ILogger<SenateScrapperService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _senateBaseUrl = _configuration["SenateApi:BaseUrl"] ?? 
                        "https://legis.senado.leg.br/dadosabertos/senador/lista/atual.json";

        // Configure HttpClient timeout
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<SenateApiResponse?> FetchSenatorsDataAsync()
    {
        try
        {
            _logger.LogInformation("Starting Senate API data fetch...");
            
            var response = await _httpClient.GetAsync(_senateBaseUrl);
            response.EnsureSuccessStatusCode();
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogWarning("Empty response received from Senate API");
                return null;
            }

            _logger.LogInformation("Successfully fetched JSON data. Size: {Size} characters", jsonContent.Length);
            
            return ParseSenateJson(jsonContent);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching Senate data");
            throw new InvalidOperationException("Failed to fetch data from Senate API", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout while fetching Senate data");
            throw new InvalidOperationException("Timeout while fetching Senate API data", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching Senate data");
            throw;
        }
    }

    public async Task<bool> IsApiAvailableAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(_senateBaseUrl, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Senate API is not available");
            return false;
        }
    }

    private SenateApiResponse? ParseSenateJson(string jsonContent)
    {
        try
        {
            _logger.LogInformation("Starting JSON parsing...");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = JsonSerializer.Deserialize<SenateApiResponse>(jsonContent, options);
            
            if (response?.ListaParlamentarEmExercicio?.Parlamentares?.Parlamentar != null)
            {
                _logger.LogInformation("Successfully parsed {Count} parliamentarians", 
                    response.ListaParlamentarEmExercicio.Parlamentares.Parlamentar.Count);
            }
            else
            {
                _logger.LogWarning("No parliamentarians found in JSON response");
            }

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error");
            throw new InvalidOperationException("Invalid JSON format received from Senate API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Senate API JSON response");
            throw;
        }
    }
}