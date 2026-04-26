namespace GiftTogether.DTOs;

public record CreateRegistryRequest(string Name, string Description);

public record UpdateRegistryRequest(string? Name, string? Description, string? HeroBackgroundColor, string? HeroImageUrl);

public record UpdateGiftGoalRequest(
    string? Name,
    string? Description,
    decimal? TargetAmount,
    string? ProductLink
);

public record CreatorInfo(
    string Name,
    string? ProfileImageUrl,
    string? GuestMessage
);

public record RegistryResponse(
    int Id,
    string Name,
    string Description,
    string Slug,
    DateTime CreatedAt,
    string? HeroBackgroundColor,
    string? HeroImageUrl,
    CreatorInfo Creator,
    List<GiftGoalResponse> GiftGoals
);

public record CreateGiftGoalRequest(
    string Name,
    string Description,
    decimal TargetAmount,
    string? ImageUrl,
    string? ProductLink
);

public record GiftGoalResponse(
    int Id,
    string Name,
    string Description,
    decimal TargetAmount,
    decimal TotalRaised,
    string? ImageUrl,
    string? ProductLink,
    List<ContributionResponse> Contributions
);

public record CreateContributionRequest(
    string ContributorName,
    string Message,
    decimal Amount
);

public record ContributionResponse(
    int Id,
    string ContributorName,
    string Message,
    decimal Amount,
    DateTime CreatedAt
);
