namespace GiftTogether.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>URL to the creator's profile photo (optional).</summary>
    public string? ProfileImageUrl { get; set; }

    /// <summary>A short personal message shown to guests on the public registry page.</summary>
    public string? GuestMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Registry> Registries { get; set; } = new List<Registry>();
}
