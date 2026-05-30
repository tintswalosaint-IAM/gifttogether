using HtmlAgilityPack;

namespace GiftTogether.Services.Scraping;

public interface IRetailerStrategy
{
    bool Matches(string host);
    (string? Title, decimal? Price, string? ImageUrl) Extract(HtmlDocument doc);
}
