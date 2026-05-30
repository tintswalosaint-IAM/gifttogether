using GiftTogether.DTOs;
using GiftTogether.Services.Scraping;
using HtmlAgilityPack;

namespace GiftTogether.Services;

public class ScraperService
{
    private readonly HttpClient _http;

    private readonly IReadOnlyList<IRetailerStrategy> _strategies = new List<IRetailerStrategy>
    {
        new TakealotStrategy(),
        new WoolworthsStrategy(),
        new CheckersStrategy(),
        new GameStrategy(),
        new MakroStrategy()
    };

    public static readonly IReadOnlySet<string> AllowedHosts =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "www.takealot.com", "takealot.com",
            "www.woolworths.co.za", "woolworths.co.za",
            "www.checkers.co.za", "checkers.co.za",
            "www.game.co.za", "game.co.za",
            "www.makro.co.za", "makro.co.za"
        };

    public ScraperService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ScrapeResultDto> ScrapeAsync(string url)
    {
        var html = await _http.GetStringAsync(url);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var uri = new Uri(url);
        var strategy = _strategies.FirstOrDefault(s => s.Matches(uri.Host));

        if (strategy == null)
            return new ScrapeResultDto(null, null, null);

        var (title, price, imageUrl) = strategy.Extract(doc);
        return new ScrapeResultDto(title, price, imageUrl);
    }
}
