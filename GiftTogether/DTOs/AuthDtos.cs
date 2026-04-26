namespace GiftTogether.DTOs;

public record RegisterRequest(
    string Name,
    string Email,
    string Password,
    string? ProfileImageUrl,
    string? GuestMessage
);

public record LoginRequest(string Email, string Password);

public record AuthResponse(int UserId, string Name, string Email, string Token);

public record UpdateProfileRequest(
    string? ProfileImageUrl,
    string? GuestMessage
);
