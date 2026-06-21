using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ChecaAI.Application.Interfaces;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Infrastructure.Services;

public class CguService : ICguService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CguService> _logger;
    private const string CGU_API_BASE = "https://portaldatransparencia.gov.br/api-de-dados";

    public CguService(HttpClient httpClient, IConfiguration configuration, ILogger<CguService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Set auth header from configuration — required by Portal da Transparência API
        var apiKey = configuration["CguApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("chave-api-dados", apiKey);
        else
            _logger.LogWarning("[CguService] CguApiKey not configured — requests will be rejected by CGU API");
    }

    /// <summary>
    /// Fetches salary records for a politician identified by CPF for a given year/month.
    /// Endpoint: GET /servidores/remuneracao?cpfCnpj={cpf}&mesAno={AAAAMM}&pagina=1&tamanhoPagina=10
    /// </summary>
    public async Task<IEnumerable<PoliticianSalary>> GetPoliticianSalariesAsync(string cpf, int? year = null, int? month = null)
    {
        try
        {
            var mesAno = (year.HasValue && month.HasValue)
                ? $"{year:D4}{month:D2}"
                : DateTime.UtcNow.ToString("yyyyMM");

            var url = $"{CGU_API_BASE}/servidores/remuneracao?cpfCnpj={cpf}&mesAno={mesAno}&pagina=1&tamanhoPagina=10";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("[CguService] HTTP {Status} for CPF {Cpf} mesAno {MesAno}", response.StatusCode, cpf, mesAno);
                return Enumerable.Empty<PoliticianSalary>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var records = JsonSerializer.Deserialize<List<CguSalaryDto>>(json, JsonOpts);

            if (records == null || records.Count == 0)
                return Enumerable.Empty<PoliticianSalary>();

            return records.Select(r => new PoliticianSalary
            {
                Month = month ?? DateTime.UtcNow.Month,
                Year = year ?? DateTime.UtcNow.Year,
                GrossSalary = r.RemuneracaoBasicaBruta,
                NetSalary = r.RendimentoLiquido,
                Allowances = r.OutrasRemuneracoes,
                Source = "CGU"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CguService] Error fetching salaries from CGU for CPF {Cpf}", cpf);
            return Enumerable.Empty<PoliticianSalary>();
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
}

file sealed class CguSalaryDto
{
    [JsonPropertyName("ano")]
    public int Ano { get; set; }

    [JsonPropertyName("mes")]
    public int Mes { get; set; }

    [JsonPropertyName("remuneracaoBasicaBruta")]
    public decimal RemuneracaoBasicaBruta { get; set; }

    [JsonPropertyName("rendimentoLiquido")]
    public decimal RendimentoLiquido { get; set; }

    [JsonPropertyName("outrasRemuneracoes")]
    public decimal OutrasRemuneracoes { get; set; }
}
