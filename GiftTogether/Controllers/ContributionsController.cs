using GiftTogether.Data;
using GiftTogether.DTOs;
using GiftTogether.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GiftTogether.Controllers;

[ApiController]
[Route("api/goals")]
public class ContributionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ContributionsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Guest submits a (fake/test) contribution to a gift goal.
    /// No authentication required.
    /// </summary>
    [HttpPost("{goalId:int}/contributions")]
    public async Task<IActionResult> Contribute(int goalId, [FromBody] CreateContributionRequest req)
    {
        var goal = await _db.GiftGoals
            .Include(g => g.Contributions)
            .FirstOrDefaultAsync(g => g.Id == goalId);

        if (goal == null) return NotFound(new { error = "Gift goal not found." });

        if (req.Amount <= 0)
            return BadRequest(new { error = "Contribution amount must be greater than zero." });

        var contribution = new Contribution
        {
            ContributorName = string.IsNullOrWhiteSpace(req.ContributorName)
                ? "Anonymous"
                : req.ContributorName.Trim(),
            Message = req.Message?.Trim() ?? string.Empty,
            Amount = req.Amount,
            GiftGoalId = goalId
        };

        _db.Contributions.Add(contribution);
        await _db.SaveChangesAsync();

        return Ok(new ContributionResponse(
            contribution.Id,
            contribution.ContributorName,
            contribution.Message,
            contribution.Amount,
            contribution.CreatedAt
        ));
    }
}
