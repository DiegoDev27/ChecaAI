using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

/// <summary>
/// Scrapper for Santa Catarina (ALESC).
/// Attempts static HTML parse from ALESC deputy list; logs HTML on failure.
/// </summary>
public class AlescScrapperService : HtmlScrapperBase, IStateDeputyScrapperService
{
    public string StateCode => "SC";
    public string AssemblyName => "ALESC";

    private static readonly string[] CandidateUrls =
    [
        "https://www.alesc.sc.leg.br/deputados",
        "https://www.alesc.sc.leg.br/parlamentar/deputados",
        "https://www.alesc.sc.leg.br/deputados/ativos",
        "https://www.alesc.sc.leg.br/transparencia/parlamentares"
    ];

    public AlescScrapperService(IHttpClientFactory httpClientFactory, ILogger<AlescScrapperService> logger)
        : base(httpClientFactory.CreateClient("StateDeputy"), logger)
    {
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        foreach (var url in CandidateUrls)
        {
            try
            {
                Logger.LogInformation("Trying ALESC URL: {Url}", url);
                var doc = await FetchHtmlAsync(url);
                if (doc == null) continue;

                var deputies = TryParseDeputies(doc);
                if (deputies.Count > 0)
                {
                    Logger.LogInformation("Successfully fetched {Count} deputies from ALESC via {Url}", deputies.Count, url);
                    return deputies;
                }

                Logger.LogWarning("No deputies parsed from ALESC at {Url}", url);
                LogRawHtml(doc, AssemblyName);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "ALESC URL {Url} failed, trying next", url);
            }
        }

        Logger.LogWarning("Could not fetch deputies from ALESC (SC)");
        return [];
    }

    public async Task<bool> IsSourceAvailableAsync()
    {
        foreach (var url in CandidateUrls)
        {
            try
            {
                using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode) return true;
            }
            catch { /* try next */ }
        }
        return false;
    }

    private List<StateDeputyData> TryParseDeputies(HtmlDocument doc)
    {
        var deputies = new List<StateDeputyData>();

        // Card patterns common in modern CMS-based assembly sites
        var cards = doc.DocumentNode.SelectNodes(
            "//div[contains(@class,'deputado')]|//div[contains(@class,'parlamentar')]" +
            "|//div[contains(@class,'vereador')]|//article[contains(@class,'card')]" +
            "|//li[contains(@class,'deputado')]");

        if (cards != null)
        {
            foreach (var card in cards)
            {
                var nameNode = card.SelectSingleNode(
                    ".//*[contains(@class,'nome')]|.//*[contains(@class,'name')]|.//h2|.//h3|.//h4|.//strong");
                var name = CleanText(nameNode?.InnerText);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var partyNode = card.SelectSingleNode(
                    ".//*[contains(@class,'partido')]|.//*[contains(@class,'party')]|.//*[contains(@class,'sigla')]");

                deputies.Add(new StateDeputyData
                {
                    ExternalId = name.ToLowerInvariant(),
                    FullName = name,
                    Party = CleanText(partyNode?.InnerText),
                    StateCode = StateCode
                });
            }
        }

        if (deputies.Count == 0)
        {
            var rows = doc.DocumentNode.SelectNodes("//table//tr[td]");
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var cells = row.SelectNodes("td");
                    if (cells == null || cells.Count < 1) continue;
                    var name = CleanText(cells[0].InnerText);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    deputies.Add(new StateDeputyData
                    {
                        ExternalId = name.ToLowerInvariant(),
                        FullName = name,
                        Party = cells.Count > 1 ? CleanText(cells[1].InnerText) : null,
                        StateCode = StateCode
                    });
                }
            }
        }

        return deputies;
    }
}
