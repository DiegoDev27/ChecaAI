using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ChecaAI.Application.Interfaces;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Infrastructure.Services;

public class WebScrapingService : IWebScrapingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebScrapingService> _logger;

    // Dictionary of supported cities and their chamber website patterns
    private readonly Dictionary<string, ChamberInfo> _supportedCities = new()
    {
        // Example entries - these would need to be configured based on actual municipal chamber websites
        { "SAO_PAULO_SP", new ChamberInfo("https://www.saopaulo.sp.leg.br", "São Paulo", "SP") },
        { "RIO_DE_JANEIRO_RJ", new ChamberInfo("https://www.camara.rj.gov.br", "Rio de Janeiro", "RJ") },
        { "BELO_HORIZONTE_MG", new ChamberInfo("https://www.cmbh.mg.gov.br", "Belo Horizonte", "MG") }
    };

    public WebScrapingService(HttpClient httpClient, ILogger<WebScrapingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Configure HttpClient with appropriate headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }

    public async Task<bool> IsCitySupportedAsync(string cityName, string stateCode)
    {
        var key = $"{cityName.ToUpper().Replace(" ", "_")}_{stateCode.ToUpper()}";
        return _supportedCities.ContainsKey(key);
    }

    public async Task<IEnumerable<Politician>> GetCouncilorsFromCityAsync(string cityName, string stateCode)
    {
        var key = $"{cityName.ToUpper().Replace(" ", "_")}_{stateCode.ToUpper()}";
        
        if (!_supportedCities.TryGetValue(key, out var chamberInfo))
        {
            _logger.LogWarning("City {CityName}-{StateCode} is not supported for web scraping", cityName, stateCode);
            return Enumerable.Empty<Politician>();
        }

        try
        {
            // This is a generic implementation - would need customization for each city
            return await ScrapeCouncilorsGeneric(chamberInfo, cityName, stateCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping councilors from {CityName}-{StateCode}", cityName, stateCode);
            return Enumerable.Empty<Politician>();
        }
    }

    public async Task<IEnumerable<VotingSession>> GetVotingSessionsFromCityAsync(string cityName, string stateCode)
    {
        var key = $"{cityName.ToUpper().Replace(" ", "_")}_{stateCode.ToUpper()}";
        
        if (!_supportedCities.TryGetValue(key, out var chamberInfo))
        {
            _logger.LogWarning("City {CityName}-{StateCode} is not supported for web scraping", cityName, stateCode);
            return Enumerable.Empty<VotingSession>();
        }

        try
        {
            return await ScrapeVotingSessionsGeneric(chamberInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping voting sessions from {CityName}-{StateCode}", cityName, stateCode);
            return Enumerable.Empty<VotingSession>();
        }
    }

    public async Task<IEnumerable<Proposal>> GetProposalsFromCityAsync(string cityName, string stateCode)
    {
        var key = $"{cityName.ToUpper().Replace(" ", "_")}_{stateCode.ToUpper()}";
        
        if (!_supportedCities.TryGetValue(key, out var chamberInfo))
        {
            _logger.LogWarning("City {CityName}-{StateCode} is not supported for web scraping", cityName, stateCode);
            return Enumerable.Empty<Proposal>();
        }

        try
        {
            return await ScrapeProposalsGeneric(chamberInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping proposals from {CityName}-{StateCode}", cityName, stateCode);
            return Enumerable.Empty<Proposal>();
        }
    }

    private async Task<IEnumerable<Politician>> ScrapeCouncilorsGeneric(ChamberInfo chamberInfo, string cityName, string stateCode)
    {
        var councilors = new List<Politician>();

        // Add delay to be respectful to the website
        await Task.Delay(1000);

        // This is a generic template - would need customization for each city's website structure
        var html = await _httpClient.GetStringAsync($"{chamberInfo.BaseUrl}/vereadores");
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Generic selectors - would need to be customized for each website
        var councilorsNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'vereador')] | //div[contains(@class, 'councilor')] | //tr[contains(@class, 'member')]");

        if (councilorsNodes != null)
        {
            foreach (var node in councilorsNodes)
            {
                try
                {
                    var name = ExtractText(node, ".//h3 | .//h4 | .//td[1] | .//span[contains(@class, 'name')]");
                    var party = ExtractText(node, ".//span[contains(@class, 'party')] | .//td[2]");

                    if (!string.IsNullOrEmpty(name))
                    {
                        councilors.Add(new Politician
                        {
                            FullName = name.Trim(),
                            PoliticalPosition = "City Councilor",
                            Party = party?.Trim(),
                            State = stateCode,
                            City = cityName,
                            IsActive = true,
                            ExternalId = $"{cityName}_{stateCode}_{name}".Replace(" ", "_")
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing councilor node from {CityName}", cityName);
                }
            }
        }

        return councilors;
    }

    private async Task<IEnumerable<VotingSession>> ScrapeVotingSessionsGeneric(ChamberInfo chamberInfo)
    {
        var sessions = new List<VotingSession>();

        await Task.Delay(1000);

        try
        {
            var html = await _httpClient.GetStringAsync($"{chamberInfo.BaseUrl}/sessoes");
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Generic implementation - would need customization
            var sessionNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'sessao')] | //tr[contains(@class, 'session')]");

            if (sessionNodes != null)
            {
                foreach (var node in sessionNodes.Take(10)) // Limit to recent sessions
                {
                    try
                    {
                        var description = ExtractText(node, ".//h4 | .//td[1] | .//span[contains(@class, 'description')]");
                        var dateText = ExtractText(node, ".//span[contains(@class, 'date')] | .//td[2]");

                        if (!string.IsNullOrEmpty(description) && DateTime.TryParse(dateText, out var votingDate))
                        {
                            sessions.Add(new VotingSession
                            {
                                Description = description.Trim(),
                                VotingDate = votingDate,
                                Chamber = $"Chamber of {chamberInfo.CityName}",
                                ExternalId = $"{chamberInfo.CityName}_{votingDate:yyyyMMdd}_{sessions.Count}",
                                Result = "Unknown"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing session node from {CityName}", chamberInfo.CityName);
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not fetch sessions page for {CityName}", chamberInfo.CityName);
        }

        return sessions;
    }

    private async Task<IEnumerable<Proposal>> ScrapeProposalsGeneric(ChamberInfo chamberInfo)
    {
        var proposals = new List<Proposal>();

        await Task.Delay(1000);

        try
        {
            var html = await _httpClient.GetStringAsync($"{chamberInfo.BaseUrl}/proposicoes");
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Generic implementation - would need customization
            var proposalNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'proposicao')] | //tr[contains(@class, 'proposal')]");

            if (proposalNodes != null)
            {
                foreach (var node in proposalNodes.Take(20)) // Limit to recent proposals
                {
                    try
                    {
                        var title = ExtractText(node, ".//h4 | .//td[1] | .//span[contains(@class, 'title')]");
                        var type = ExtractText(node, ".//span[contains(@class, 'type')] | .//td[2]");
                        var dateText = ExtractText(node, ".//span[contains(@class, 'date')] | .//td[3]");

                        if (!string.IsNullOrEmpty(title))
                        {
                            proposals.Add(new Proposal
                            {
                                Title = title.Trim(),
                                Type = type?.Trim() ?? "Bill",
                                Chamber = $"Chamber of {chamberInfo.CityName}",
                                Year = DateTime.Now.Year,
                                Status = "In Process",
                                ExternalId = $"{chamberInfo.CityName}_{title}".Replace(" ", "_"),
                                ProposalDate = DateTime.TryParse(dateText, out var proposalDate) ? proposalDate : null
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing proposal node from {CityName}", chamberInfo.CityName);
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not fetch proposals page for {CityName}", chamberInfo.CityName);
        }

        return proposals;
    }

    private static string? ExtractText(HtmlNode node, string xpath)
    {
        var targetNode = node.SelectSingleNode(xpath);
        return targetNode?.InnerText?.Trim();
    }
}

public class ChamberInfo
{
    public string BaseUrl { get; set; }
    public string CityName { get; set; }
    public string StateCode { get; set; }

    public ChamberInfo(string baseUrl, string cityName, string stateCode)
    {
        BaseUrl = baseUrl;
        CityName = cityName;
        StateCode = stateCode;
    }
}