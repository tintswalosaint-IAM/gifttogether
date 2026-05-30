using HtmlAgilityPack;

namespace GiftTogether.Services.Scraping;

public class TakealotStrategy : StrategyBase
{
    public override bool Matches(string host) =>
        host.EndsWith("takealot.com", StringComparison.OrdinalIgnoreCase);

    public override (string? Title, decimal? Price, string? ImageUrl) Extract(HtmlDocument doc)
    {
        // Title
        var title = doc.DocumentNode.SelectSingleNode("//h1[contains(@class,'pdp-title')]")?.InnerText?.Trim()
            ?? CleanTitle(PageTitle(doc) ?? OgTitle(doc), " | Takealot.com");

        // Price
        var priceRaw = doc.DocumentNode.SelectSingleNode("//span[contains(@class,'currency') and contains(@class,'plus')]")?.InnerText
            ?? MetaPrice(doc);
        var price = ParseZarPrice(priceRaw);

        // Image
        var imageUrl = doc.DocumentNode.SelectSingleNode("//img[contains(@class,'pdp-image')]")?.GetAttributeValue("src", null)
            ?? OgImage(doc);

        return (title, price, imageUrl);
    }

    private static string? CleanTitle(string? title, string suffix) =>
        title == null ? null : title.Replace(suffix, "", StringComparison.OrdinalIgnoreCase).Trim();
}
