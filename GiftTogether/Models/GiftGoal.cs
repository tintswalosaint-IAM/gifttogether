namespace GiftTogether.Models;

public class GiftGoal
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }

    /// <summary>URL to a product image (external URL or uploaded, optional).</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Link to the product or store page (optional).</summary>
    public string? ProductLink { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int RegistryId { get; set; }
    public Registry Registry { get; set; } = null!;

    public ICollection<Contribution> Contributions { get; set; } = new List<Contribution>();
}
