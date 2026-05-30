using HtmlAgilityPack;

namespace GiftTogether.Services.Scraping;

public class CheckersStrategy : StrategyBase
{
    public override bool Matches(string host) =>
        host.EndsWith("checkers.co.za", StringComparison.OrdinalIgnoreCase);

    public override (string? Title, decimal? Price, string? ImageUrl) Extract(HtmlDocument doc)
    {
        var title = doc.DocumentNode.SelectSingleNode("//h1[contains(@class,'pdp__name')]")?.InnerText?.Trim()
            ?? PageTitle(doc) ?? OgTitle(doc);

        var priceRaw = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'pdp__price')]")?.InnerText
            ?? MetaPrice(doc);
        var price = ParseZarPrice(priceRaw);

        var imageUrl = OgImage(doc)
            ?? doc.DocumentNode.SelectSingleNode("//img[contains(@class,'pdp__image')]")?.GetAttributeValue("src", null);

        return (title, price, imageUrl);
    }
}
