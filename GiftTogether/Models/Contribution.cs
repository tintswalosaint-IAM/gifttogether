namespace GiftTogether.Models;

public class Contribution
{
    public int Id { get; set; }
    public string ContributorName { get; set; } = "Anonymous";
    public string Message { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int GiftGoalId { get; set; }
    public GiftGoal GiftGoal { get; set; } = null!;
}
