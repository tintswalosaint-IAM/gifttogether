using GiftTogether.Data;
using GiftTogether.Models;
using GiftTogether.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GiftTogether.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PaystackService _paystack;
    private readonly TokenService _tokens;
    private readonly IConfiguration _config;

    public PaymentsController(
        AppDbContext db,
        PaystackService paystack,
        TokenService tokens,
        IConfiguration config)
    {
        _db = db;
        _paystack = paystack;
        _tokens = tokens;
        _config = config;
    }

    // ── POST /api/payments/quote ──────────────────────────────────────────────
    // Public — no auth required. Returns fee breakdown for a given amount.
    // Used by the frontend to show fees before the contributor clicks pay.

    [HttpPost("quote")]
    public IActionResult Quote([FromBody] QuoteRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest(new { error = "Amount must be greater than zero." });

        var breakdown = PaystackService.CalculateFeeBreakdown(req.Amount);
        return Ok(new
        {
            contributionAmount = breakdown.ContributionAmount,
            neoServiceFee = breakdown.NeoServiceFee,
            paystackProcessingFee = breakdown.PaystackProcessingFee,
            totalCharged = breakdown.TotalCharged,
            paystackAmountInCents = (int)(breakdown.TotalCharged * 100)
        });
    }

    // ── GET /api/payments/banks ───────────────────────────────────────────────
    // Public — no auth required. Used to populate bank dropdown.

    [HttpGet("banks")]
    public async Task<IActionResult> GetBanks()
    {
        try
        {
            var banks = await _paystack.GetBanksAsync();
            return Ok(banks);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = ex.Message });
        }
    }

    // ── POST /api/payments/register-bank ─────────────────────────────────────
    // Auth required. Creator registers their bank account for payouts.

    [HttpPost("register-bank")]
    public async Task<IActionResult> RegisterBank([FromBody] RegisterBankRequest req)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        if (string.IsNullOrWhiteSpace(req.BankCode) ||
            string.IsNullOrWhiteSpace(req.AccountNumber) ||
            string.IsNullOrWhiteSpace(req.AccountHolderName))
            return BadRequest(new { error = "Bank code, account number, and account holder name are required." });

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound(new { error = "User not found." });

        try
        {
            var subaccountCode = await _paystack.CreateSubaccountAsync(
                user.Name,
                req.BankCode,
                req.AccountNumber,
                req.AccountType,
                user.PaystackSubaccountCode);

            user.PaystackSubaccountCode = subaccountCode;
            await _db.SaveChangesAsync();

            return Ok(new { subaccountCode });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = ex.Message });
        }
    }

    // ── POST /api/payments/initialize ────────────────────────────────────────
    // Public — no auth required. Contributor initiates payment.

    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] InitializePaymentRequest req)
    {
        if (req.GoalId <= 0 || req.ContributionAmount <= 0)
            return BadRequest(new { error = "Valid goalId and contributionAmount are required." });

        var goal = await _db.GiftGoals
            .Include(g => g.Registry)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(g => g.Id == req.GoalId);

        if (goal == null)
            return NotFound(new { error = "Gift goal not found." });

        var creator = goal.Registry.User;
        var subaccountCode = creator.PaystackSubaccountCode;

        // Use the same CalculateFeeBreakdown as the quote endpoint — single source of truth
        var breakdown = PaystackService.CalculateFeeBreakdown(req.ContributionAmount);
        var reference = $"NEO-{req.GoalId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{Guid.NewGuid().ToString("N")[..6]}";

        // Persist pending payment before calling Paystack
        var pending = new PendingPayment
        {
            Reference = reference,
            GiftGoalId = req.GoalId,
            ContributionAmount = req.ContributionAmount,
            GrossAmount = breakdown.TotalCharged,
            ContributorName = string.IsNullOrWhiteSpace(req.ContributorName) ? null : req.ContributorName.Trim(),
            ContributorMessage = string.IsNullOrWhiteSpace(req.Message) ? null : req.Message.Trim(),
            Status = PendingPaymentStatus.Pending
        };
        _db.PendingPayments.Add(pending);
        await _db.SaveChangesAsync();

        var callbackBase = _config["Paystack:CallbackBaseUrl"] ?? "http://localhost:5150";
        var callbackUrl = $"{callbackBase}/r/{goal.Registry.Slug}?reference={reference}";

        var email = string.IsNullOrWhiteSpace(req.Email)
            ? "contributor@neo.gift"
            : req.Email.Trim();

        try
        {
            var (accessCode, authorizationUrl) = await _paystack.InitializeTransactionAsync(
                reference,
                breakdown.TotalCharged,
                email,
                callbackUrl,
                subaccountCode,
                req.ContributionAmount);

            return Ok(new
            {
                reference,
                accessCode,
                authorizationUrl,
                contributionAmount = breakdown.ContributionAmount,
                neoServiceFee = breakdown.NeoServiceFee,
                paystackProcessingFee = breakdown.PaystackProcessingFee,
                totalCharged = breakdown.TotalCharged,
                paystackAmountInCents = (int)(breakdown.TotalCharged * 100)
            });
        }
        catch (Exception ex)
        {
            pending.Status = PendingPaymentStatus.Failed;
            await _db.SaveChangesAsync();
            return StatusCode(502, new { error = ex.Message });
        }
    }

    // ── POST /api/payments/verify/{reference} ─────────────────────────────────
    // Public — no auth required. Called by frontend after returning from Paystack.

    [HttpPost("verify/{reference}")]
    public async Task<IActionResult> Verify(string reference)
    {
        var pending = await _db.PendingPayments
            .FirstOrDefaultAsync(p => p.Reference == reference);

        if (pending == null)
            return NotFound(new { error = "Payment reference not found." });

        // Idempotent — already confirmed
        if (pending.Status == PendingPaymentStatus.Confirmed)
        {
            var existing = await _db.Contributions
                .FirstOrDefaultAsync(c => c.PaystackReference == reference);
            return Ok(new { alreadyConfirmed = true, contribution = existing });
        }

        try
        {
            var txn = await _paystack.VerifyTransactionAsync(reference);

            if (txn.Status != "success")
            {
                pending.Status = PendingPaymentStatus.Failed;
                await _db.SaveChangesAsync();
                return StatusCode(402, new { error = $"Payment status: {txn.Status}. Not confirmed." });
            }

            return await ConfirmPaymentAsync(pending);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = ex.Message });
        }
    }

    // ── POST /api/payments/webhook ────────────────────────────────────────────
    // Paystack webhook — source of truth for payment confirmation.

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        Request.EnableBuffering();
        using var reader = new System.IO.StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var signature = Request.Headers["x-paystack-signature"].FirstOrDefault() ?? "";

        var webhookSecret = _config["Paystack:WebhookSecret"] ?? "";
        if (!string.IsNullOrEmpty(webhookSecret))
        {
            if (!_paystack.VerifyWebhookSignature(rawBody, signature))
                return BadRequest(new { error = "Invalid webhook signature." });
        }

        JsonElement payload;
        try { payload = JsonSerializer.Deserialize<JsonElement>(rawBody); }
        catch { return BadRequest(new { error = "Invalid JSON payload." }); }

        var eventType = payload.TryGetProperty("event", out var ev) ? ev.GetString() : null;
        if (eventType != "charge.success") return Ok();

        var reference = payload.GetProperty("data").GetProperty("reference").GetString();
        if (string.IsNullOrEmpty(reference)) return Ok();

        var pending = await _db.PendingPayments
            .FirstOrDefaultAsync(p => p.Reference == reference);

        if (pending == null || pending.Status == PendingPaymentStatus.Confirmed)
            return Ok();

        await ConfirmPaymentAsync(pending);
        return Ok();
    }

    // ── Shared confirmation logic ─────────────────────────────────────────────

    private async Task<IActionResult> ConfirmPaymentAsync(PendingPayment pending)
    {
        var contribution = new Contribution
        {
            ContributorName = pending.ContributorName ?? "Anonymous",
            Message = pending.ContributorMessage ?? string.Empty,
            Amount = pending.ContributionAmount,
            GiftGoalId = pending.GiftGoalId,
            PaystackReference = pending.Reference
        };

        _db.Contributions.Add(contribution);
        pending.Status = PendingPaymentStatus.Confirmed;
        await _db.SaveChangesAsync();

        var goal = await _db.GiftGoals
            .Include(g => g.Contributions)
            .FirstOrDefaultAsync(g => g.Id == pending.GiftGoalId);

        var totalRaised = goal?.Contributions.Sum(c => c.Amount) ?? 0;

        return Ok(new
        {
            confirmed = true,
            contributorName = contribution.ContributorName,
            amount = contribution.Amount,
            reference = contribution.PaystackReference,
            totalRaised
        });
    }

    private int? GetCurrentUserId()
    {
        var auth = Request.Headers.Authorization.FirstOrDefault();
        if (auth == null || !auth.StartsWith("Bearer ")) return null;
        return _tokens.ValidateToken(auth["Bearer ".Length..]);
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record QuoteRequest(decimal Amount);

public record InitializePaymentRequest(
    int GoalId,
    decimal ContributionAmount,
    string? ContributorName,
    string? Message,
    string? Email);

public record RegisterBankRequest(
    string BankCode,
    string AccountNumber,
    string AccountHolderName,
    string? AccountType);
