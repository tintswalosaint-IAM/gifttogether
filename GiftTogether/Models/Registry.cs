namespace GiftTogether.Models;

public class Registry
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    /// <summary>Optional hex color for the hero background, e.g. "#6c63ff".</summary>
    public string? HeroBackgroundColor { get; set; }

    /// <summary>Optional uploaded image used as the hero background.</summary>
    public string? HeroImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<GiftGoal> GiftGoals { get; set; } = new List<GiftGoal>();
}
