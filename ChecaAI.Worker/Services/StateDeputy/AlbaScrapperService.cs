using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

/// <summary>
/// Scrapper for Bahia (ALBA — Assembleia Legislativa da Bahia).
/// The ALBA site uses a Bootstrap grid layout; deputy data may be dynamically loaded.
/// Tries multiple URL paths; logs HTML for selector inspection on first parse failure.
/// </summary>
public class AlbaScrapperService : HtmlScrapperBase, IStateDeputyScrapperService
{
    public string StateCode => "BA";
    public string AssemblyName => "ALBA";

    private static readonly string[] CandidateUrls =
    [
        "https://www.al.ba.leg.br/deputados/deputados-estaduais",
        "https://www.al.ba.leg.br/deputados/legislatura-atual",
        "https://www.al.ba.leg.br/deputados/todos-deputados",
        "https://www.al.ba.leg.br/deputados/contatos-deputados",
        "https://www.al.ba.leg.br/deputados"
    ];

    public AlbaScrapperService(IHttpClientFactory httpClientFactory, ILogger<AlbaScrapperService> logger)
        : base(httpClientFactory.CreateClient("StateDeputy"), logger)
    {
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        foreach (var url in CandidateUrls)
        {
            try
            {
                Logger.LogInformation("Trying ALBA URL: {Url}", url);
                var doc = await FetchHtmlAsync(url);
                if (doc == null) continue;

                var deputies = TryParseDeputies(doc);
                if (deputies.Count > 0)
                {
                    Logger.LogInformation("Successfully fetched {Count} deputies from ALBA via {Url}", deputies.Count, url);
                    return deputies;
                }

                Logger.LogWarning("No deputies parsed from ALBA at {Url} — may require JavaScript rendering", url);
                LogRawHtml(doc, AssemblyName);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "ALBA URL {Url} failed, trying next", url);
            }
        }

        Logger.LogWarning("Could not fetch deputies from ALBA (BA) — site may require headless browser");
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

        // Bootstrap col-md-3 cards (ALBA pattern)
        var cols = doc.DocumentNode.SelectNodes(
            "//div[contains(@class,'col-md-3')]|//div[contains(@class,'col-sm-4')]" +
            "|//div[contains(@class,'deputado')]|//div[contains(@class,'parlamentar')]");

        if (cols != null)
        {
            foreach (var col in cols)
            {
                var nameNode = col.SelectSingleNode(
                    ".//*[contains(@class,'nome')]|.//*[contains(@class,'name')]|.//h2|.//h3|.//h4|.//strong|.//a");
                var name = CleanText(nameNode?.InnerText);
                if (string.IsNullOrWhiteSpace(name) || name.Length < 5) continue;

                var partyNode = col.SelectSingleNode(
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
                    if (string.IsNullOrWhiteSpace(name) || name.Length < 5) continue;
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
