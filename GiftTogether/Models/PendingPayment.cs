namespace GiftTogether.Models;

public enum PendingPaymentStatus { Pending, Confirmed, Failed }

public class PendingPayment
{
    public int Id { get; set; }

    /// <summary>Unique reference sent to Paystack (e.g. NEO-5-1716000000-abc123).</summary>
    public string Reference { get; set; } = string.Empty;

    public int GiftGoalId { get; set; }
    public GiftGoal GiftGoal { get; set; } = null!;

    /// <summary>The amount the contributor intends to send to the creator (ZAR).</summary>
    public decimal ContributionAmount { get; set; }

    /// <summary>The gross amount charged to the contributor's card (includes NEO fee + Paystack fee gross-up).</summary>
    public decimal GrossAmount { get; set; }

    public string? ContributorName { get; set; }
    public string? ContributorMessage { get; set; }

    public PendingPaymentStatus Status { get; set; } = PendingPaymentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
