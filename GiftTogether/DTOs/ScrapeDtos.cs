namespace GiftTogether.DTOs;

public record ScrapeRequest(string Url);

public record ScrapeResultDto(
    string? Title,
    decimal? Price,
    string? ImageUrl
);
