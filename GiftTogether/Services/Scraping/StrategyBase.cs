using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace GiftTogether.Services.Scraping;

public abstract class StrategyBase : IRetailerStrategy
{
    public abstract bool Matches(string host);
    public abstract (string? Title, decimal? Price, string? ImageUrl) Extract(HtmlDocument doc);

    protected static string? OgImage(HtmlDocument doc) =>
        doc.DocumentNode
           .SelectSingleNode("//meta[@property='og:image']")
           ?.GetAttributeValue("content", null);

    protected static string? OgTitle(HtmlDocument doc) =>
        doc.DocumentNode
           .SelectSingleNode("//meta[@property='og:title']")
           ?.GetAttributeValue("content", null);

    protected static string? PageTitle(HtmlDocument doc) =>
        doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();

    protected static string? MetaPrice(HtmlDocument doc) =>
        doc.DocumentNode
           .SelectSingleNode("//meta[@property='product:price:amount']")
           ?.GetAttributeValue("content", null);

    public static decimal? ParseZarPrice(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Strip currency symbol and whitespace
        var cleaned = Regex.Replace(raw, @"[R\s]", "");
        // Remove thousand separators (comma or period before 3 digits)
        cleaned = Regex.Replace(cleaned, @"[,\.](?=\d{3}(?:[,\.]|$))", "");
        // Normalise decimal comma to period
        cleaned = cleaned.Replace(",", ".");
        // Strip any remaining non-numeric characters except decimal point
        cleaned = Regex.Replace(cleaned, @"[^\d\.]", "");
        return decimal.TryParse(cleaned,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var result) ? result : null;
    }
}
