using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

public class AlespScrapperService : IStateDeputyScrapperService
{
    public string StateCode => "SP";
    public string AssemblyName => "ALESP";

    private const string XmlUrl = "https://www.al.sp.gov.br/repositorioDados/deputados/deputados.xml";
    private const string ActiveStatus = "EXE";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AlespScrapperService> _logger;

    public AlespScrapperService(IHttpClientFactory httpClientFactory, ILogger<AlespScrapperService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("StateDeputy");
        _logger = logger;
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching deputies from ALESP XML...");

            var xmlContent = await _httpClient.GetStringAsync(XmlUrl);

            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                _logger.LogWarning("Empty response from ALESP XML endpoint");
                return [];
            }

            var doc = XDocument.Parse(xmlContent);
            var deputies = doc.Root?
                .Elements("Deputado")
                .Where(e => (string?)e.Element("Situacao") == ActiveStatus)
                .Select(MapToStateDeputyData)
                .ToList() ?? [];

            _logger.LogInformation("Successfully fetched {Count} active deputies from ALESP", deputies.Count);

            return deputies;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching ALESP XML");
            throw new InvalidOperationException("Failed to fetch data from ALESP", ex);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException { InnerException: System.Xml.XmlException })
        {
            _logger.LogError(ex, "XML parsing error for ALESP response");
            throw new InvalidOperationException("Invalid XML format received from ALESP", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching ALESP data");
            throw;
        }
    }

    public async Task<bool> IsSourceAvailableAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(XmlUrl, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ALESP source is not available");
            return false;
        }
    }

    private StateDeputyData MapToStateDeputyData(XElement el)
    {
        var id = (string?)el.Element("IdDeputado") ?? string.Empty;
        return new StateDeputyData
        {
            ExternalId = id,
            FullName = (string?)el.Element("NomeParlamentar") ?? string.Empty,
            Party = (string?)el.Element("Partido"),
            StateCode = StateCode,
            Email = (string?)el.Element("Email"),
            PhotoUrl = string.IsNullOrEmpty(id)
                ? null
                : $"https://www.al.sp.gov.br/repositorioDados/deputados/fotos/{id}.jpg",
            ParlamentaryPageUrl = null
        };
    }
}
