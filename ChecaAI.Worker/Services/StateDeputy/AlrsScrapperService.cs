using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

/// <summary>
/// Scrapper for Rio Grande do Sul (ALRS).
/// The ALRS site (ww4.al.rs.gov.br) uses JavaScript rendering for the deputy list.
/// Attempts static HTML parse first; logs raw HTML if no deputies found.
/// </summary>
public class AlrsScrapperService : HtmlScrapperBase, IStateDeputyScrapperService
{
    public string StateCode => "RS";
    public string AssemblyName => "ALRS";

    private static readonly string[] CandidateUrls =
    [
        "https://ww4.al.rs.gov.br/deputados",
        "https://ww3.al.rs.gov.br/site/deputados",
        "https://www.al.rs.gov.br/deputados",
        "https://ww4.al.rs.gov.br/deputados/ativos"
    ];

    public AlrsScrapperService(IHttpClientFactory httpClientFactory, ILogger<AlrsScrapperService> logger)
        : base(httpClientFactory.CreateClient("StateDeputy"), logger)
    {
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        foreach (var url in CandidateUrls)
        {
            try
            {
                Logger.LogInformation("Trying ALRS URL: {Url}", url);
                var doc = await FetchHtmlAsync(url);
                if (doc == null) continue;

                var deputies = TryParseDeputies(doc);
                if (deputies.Count > 0)
                {
                    Logger.LogInformation("Successfully fetched {Count} deputies from ALRS via {Url}", deputies.Count, url);
                    return deputies;
                }

                Logger.LogWarning("No deputies parsed from ALRS at {Url} — site may require JavaScript rendering", url);
                LogRawHtml(doc, AssemblyName);
                break; // if the page loaded but had no static data, no point trying other URLs
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "ALRS URL {Url} failed, trying next", url);
            }
        }

        Logger.LogWarning("Could not fetch deputies from ALRS (RS) — site likely requires JavaScript rendering");
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

        // Modern card-based layout
        var cards = doc.DocumentNode.SelectNodes(
            "//div[contains(@class,'deputado')]|//div[contains(@class,'parlamentar')]" +
            "|//article[contains(@class,'deputado')]|//li[contains(@class,'deputado')]");

        if (cards != null)
        {
            foreach (var card in cards)
            {
                var nameNode = card.SelectSingleNode(
                    ".//*[contains(@class,'nome')]|.//*[contains(@class,'name')]|.//h3|.//h4|.//strong");
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

        // Table fallback
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
