using HtmlAgilityPack;

namespace GiftTogether.Services.Scraping;

public class GameStrategy : StrategyBase
{
    public override bool Matches(string host) =>
        host.EndsWith("game.co.za", StringComparison.OrdinalIgnoreCase);

    public override (string? Title, decimal? Price, string? ImageUrl) Extract(HtmlDocument doc)
    {
        var title = doc.DocumentNode.SelectSingleNode("//h1[contains(@class,'product-name')]")?.InnerText?.Trim()
            ?? CleanTitle(PageTitle(doc) ?? OgTitle(doc), " - Game");

        var priceRaw = doc.DocumentNode.SelectSingleNode("//span[contains(@class,'price-box__price')]")?.InnerText
            ?? MetaPrice(doc);
        var price = ParseZarPrice(priceRaw);

        var imageUrl = OgImage(doc)
            ?? doc.DocumentNode.SelectSingleNode("//img[contains(@class,'product-image-photo')]")?.GetAttributeValue("src", null);

        return (title, price, imageUrl);
    }

    private static string? CleanTitle(string? title, string suffix) =>
        title == null ? null : title.Replace(suffix, "", StringComparison.OrdinalIgnoreCase).Trim();
}
