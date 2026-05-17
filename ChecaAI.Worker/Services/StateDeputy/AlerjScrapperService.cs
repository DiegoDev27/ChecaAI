using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ChecaAI.Worker.Services.StateDeputy;

/// <summary>
/// Scrapper for Rio de Janeiro (ALERJ).
/// Parses the server-rendered deputy grid at /Deputados/QuemSao.
/// Card structure: div.controle_deputado > div.descricao > div.partido + div.nome > a
/// </summary>
public class AlerjScrapperService : HtmlScrapperBase, IStateDeputyScrapperService
{
    public string StateCode => "RJ";
    public string AssemblyName => "ALERJ";

    private const string DeputiesUrl = "https://www.alerj.rj.gov.br/Deputados/QuemSao";

    public AlerjScrapperService(IHttpClientFactory httpClientFactory, ILogger<AlerjScrapperService> logger)
        : base(httpClientFactory.CreateClient("StateDeputy"), logger)
    {
    }

    public async Task<List<StateDeputyData>> FetchDeputiesAsync()
    {
        try
        {
            Logger.LogInformation("Fetching deputies from ALERJ (RJ)...");

            var doc = await FetchHtmlAsync(DeputiesUrl);
            if (doc == null)
            {
                Logger.LogWarning("Empty response from ALERJ");
                return [];
            }

            var cards = doc.DocumentNode.SelectNodes("//div[contains(@class,'controle_deputado')]");
            if (cards == null || cards.Count == 0)
            {
                Logger.LogWarning("No deputy cards found on ALERJ page — HTML structure may have changed");
                LogRawHtml(doc, AssemblyName);
                return [];
            }

            var deputies = new List<StateDeputyData>();
            foreach (var card in cards)
            {
                var nameNode = card.SelectSingleNode(".//div[@class='nome']/a");
                var partyNode = card.SelectSingleNode(".//div[@class='partido']");
                var linkNode = card.SelectSingleNode(".//div[@class='imagem']/a")
                               ?? card.SelectSingleNode(".//div[@class='nome']/a");

                var name = CleanText(nameNode?.InnerText);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var href = linkNode?.GetAttributeValue("href", "");
                var idMatch = Regex.Match(href ?? "", @"/PerfilDeputado/(\d+)");
                var externalId = idMatch.Success ? idMatch.Groups[1].Value : name.ToLowerInvariant();

                deputies.Add(new StateDeputyData
                {
                    ExternalId = externalId,
                    FullName = name,
                    Party = CleanText(partyNode?.InnerText),
                    StateCode = StateCode,
                    ParlamentaryPageUrl = !string.IsNullOrEmpty(href)
                        ? $"https://www.alerj.rj.gov.br{href}"
                        : null
                });
            }

            Logger.LogInformation("Successfully fetched {Count} deputies from ALERJ", deputies.Count);
            return deputies;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error fetching ALERJ deputies");
            return [];
        }
    }

    public async Task<bool> IsSourceAvailableAsync()
    {
        try
        {
            using var response = await HttpClient.GetAsync(DeputiesUrl, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "ALERJ source is not available");
            return false;
        }
    }
}
