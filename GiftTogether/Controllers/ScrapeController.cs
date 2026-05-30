using GiftTogether.DTOs;
using GiftTogether.Services;
using Microsoft.AspNetCore.Mvc;

namespace GiftTogether.Controllers;

[ApiController]
[Route("api/scrape")]
public class ScrapeController : ControllerBase
{
    private readonly ScraperService _scraper;
    private readonly TokenService _tokens;

    public ScrapeController(ScraperService scraper, TokenService tokens)
    {
        _scraper = scraper;
        _tokens = tokens;
    }

    [HttpPost]
    public async Task<IActionResult> Scrape([FromBody] ScrapeRequest req)
    {
        // Auth check
        var auth = Request.Headers.Authorization.FirstOrDefault();
        if (auth == null || !auth.StartsWith("Bearer "))
            return Unauthorized(new { error = "Authentication required." });

        var userId = _tokens.ValidateToken(auth["Bearer ".Length..]);
        if (userId == null)
            return Unauthorized(new { error = "Authentication required." });

        // Validate URL
        if (string.IsNullOrWhiteSpace(req.Url) ||
            !Uri.TryCreate(req.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            return BadRequest(new { error = "Invalid URL." });

        // Check allowlist
        if (!ScraperService.AllowedHosts.Contains(uri.Host))
            return UnprocessableEntity(new { error = "Unsupported retailer. Supported sites: Takealot, Woolworths, Checkers, Game, Makro." });

        // Scrape
        try
        {
            var result = await _scraper.ScrapeAsync(req.Url);
            return Ok(result);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "Could not reach the product page." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(502, new { error = "Could not reach the product page." });
        }
    }
}
