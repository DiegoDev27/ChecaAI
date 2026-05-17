using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace ChecaAI.Worker.Services.StateDeputy;

/// <summary>
/// Base class for HTML-scraping state deputy scrapers.
/// Provides shared HTML fetch logic and text normalization.
/// </summary>
public abstract class HtmlScrapperBase
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;

    protected HtmlScrapperBase(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient;
        Logger = logger;
    }

    protected async Task<HtmlDocument?> FetchHtmlAsync(string url)
    {
        var html = await HttpClient.GetStringAsync(url);
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    /// <summary>Logs raw HTML at DEBUG level to help diagnose selector failures.</summary>
    protected void LogRawHtml(HtmlDocument doc, string assemblyName, int maxChars = 3000)
    {
        var raw = doc.DocumentNode.OuterHtml;
        Logger.LogDebug("{Assembly} raw HTML (first {Max} chars): {Html}",
            assemblyName,
            maxChars,
            raw.Length > maxChars ? raw[..maxChars] : raw);
    }

    protected static string CleanText(string? text) =>
        System.Net.WebUtility.HtmlDecode((text ?? string.Empty))
            .Trim()
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();
}
