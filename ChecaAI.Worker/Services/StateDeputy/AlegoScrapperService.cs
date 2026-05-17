using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

/// <summary>
/// Scrapper for Goiás (ALEGO).
/// Attempts to parse the deputy list from the ALEGO website.
/// Uses common Brazilian assembly HTML patterns; logs raw HTML on parse failure for selector tuning.
/// </summary>
public class AlegoScrapperService : HtmlScrapperBase, IStateDeputyScrapperService
{
    public string StateCode => "GO";
    public string AssemblyName => "ALEGO";

    private static readonly string[] CandidateUrls =
    [
        "https://www.alego.go.leg.br/deputados",
        "https://www.alego.go.leg.br/deputados/lista",
        "https://www.alego.go.leg.br/parlamentar/deputados",
        "https://www.alego.go.leg.br/deputados/ativos"
    ];

    public AlegoScrapperService(IHttpClientFactory httpClientFactory, ILogger<AlegoScrapperService> logger)
        : base(httpClientFactory.CreateClient("StateDeputy"), logger)
    {
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        foreach (var url in CandidateUrls)
        {
            try
            {
                Logger.LogInformation("Trying ALEGO URL: {Url}", url);
                var doc = await FetchHtmlAsync(url);
                if (doc == null) continue;

                var deputies = TryParseDeputies(doc, url);
                if (deputies.Count > 0)
                {
                    Logger.LogInformation("Successfully fetched {Count} deputies from ALEGO via {Url}", deputies.Count, url);
                    return deputies;
                }

                Logger.LogWarning("No deputies parsed from ALEGO at {Url} — logging HTML for selector inspection", url);
                LogRawHtml(doc, AssemblyName);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "ALEGO URL {Url} failed, trying next", url);
            }
        }

        Logger.LogWarning("Could not fetch deputies from ALEGO (GO) — all URLs failed or returned empty");
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

    private List<StateDeputyData> TryParseDeputies(HtmlDocument doc, string sourceUrl)
    {
        var deputies = new List<StateDeputyData>();

        // Pattern 1: div cards with name + party (most common in modern Brazilian assemblies)
        var cards = doc.DocumentNode.SelectNodes(
            "//div[contains(@class,'deputado')]|//div[contains(@class,'parlamentar')]|//div[contains(@class,'card')]");

        if (cards != null)
        {
            foreach (var card in cards)
            {
                var name = ExtractName(card);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var party = ExtractParty(card);
                deputies.Add(new StateDeputyData
                {
                    ExternalId = name.ToLowerInvariant(),
                    FullName = name,
                    Party = party,
                    StateCode = StateCode
                });
            }
        }

        // Pattern 2: table rows
        if (deputies.Count == 0)
        {
            var rows = doc.DocumentNode.SelectNodes("//table//tr[td]");
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var cells = row.SelectNodes("td");
                    if (cells == null || cells.Count < 2) continue;

                    var name = CleanText(cells[0].InnerText);
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    deputies.Add(new StateDeputyData
                    {
                        ExternalId = name.ToLowerInvariant(),
                        FullName = name,
                        Party = CleanText(cells[1].InnerText),
                        StateCode = StateCode
                    });
                }
            }
        }

        return deputies;
    }

    private static string? ExtractName(HtmlNode card)
    {
        var nameNode = card.SelectSingleNode(
            ".//*[contains(@class,'nome')]|.//*[contains(@class,'name')]|.//*[contains(@class,'titulo')]");
        if (nameNode != null)
            return CleanText(nameNode.InnerText);

        // Fallback: longest text node in the card that looks like a name
        var text = CleanText(card.SelectSingleNode(".//h2|.//h3|.//h4|.//strong")?.InnerText);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? ExtractParty(HtmlNode card)
    {
        var partyNode = card.SelectSingleNode(
            ".//*[contains(@class,'partido')]|.//*[contains(@class,'party')]|.//*[contains(@class,'legenda')]");
        return partyNode != null ? CleanText(partyNode.InnerText) : null;
    }
}
