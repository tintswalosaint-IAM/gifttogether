using GiftTogether.Data;
using GiftTogether.DTOs;
using GiftTogether.Models;
using GiftTogether.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GiftTogether.Controllers;

[ApiController]
[Route("api/registries")]
public class RegistriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    private readonly IWebHostEnvironment _env;

    public RegistriesController(AppDbContext db, TokenService tokens, IWebHostEnvironment env)
    {
        _db = db;
        _tokens = tokens;
        _env = env;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts and validates the userId from the Bearer token.
    /// Returns null (caller should return 401) if the token is missing,
    /// invalid, expired, or the userId does not exist in the database.
    /// This prevents FK constraint failures from stale tokens pointing at
    /// deleted users.
    /// </summary>
    private async Task<int?> GetVerifiedUserIdAsync()
    {
        var auth = Request.Headers.Authorization.FirstOrDefault();
        if (auth == null || !auth.StartsWith("Bearer ")) return null;

        var userId = _tokens.ValidateToken(auth["Bearer ".Length..]);
        if (userId == null) return null;

        // Confirm the user actually exists — a structurally valid token can
        // still reference a user that was deleted or never committed (e.g.
        // after a database reset during development).
        var exists = await _db.Users.AnyAsync(u => u.Id == userId.Value);
        return exists ? userId : null;
    }

    // Synchronous variant for read-only endpoints where we don't need the DB check
    private int? GetCurrentUserId()
    {
        var auth = Request.Headers.Authorization.FirstOrDefault();
        if (auth == null || !auth.StartsWith("Bearer ")) return null;
        return _tokens.ValidateToken(auth["Bearer ".Length..]);
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLower()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "");
        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        slug = slug.Trim('-');
        var suffix = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("=", "").Replace("+", "").Replace("/", "")[..6];
        return $"{slug}-{suffix}";
    }

    private static RegistryResponse MapRegistry(Registry r) => new(
        r.Id, r.Name, r.Description, r.Slug, r.CreatedAt,
        r.HeroBackgroundColor,
        r.HeroImageUrl,
        new CreatorInfo(r.User.Name, r.User.ProfileImageUrl, r.User.GuestMessage),
        r.GiftGoals.Select(MapGoal).ToList()
    );

    private static GiftGoalResponse MapGoal(GiftGoal g) => new(
        g.Id, g.Name, g.Description, g.TargetAmount,
        g.Contributions.Sum(c => c.Amount),
        g.ImageUrl,
        g.ProductLink,
        g.Contributions.Select(MapContribution).OrderByDescending(c => c.CreatedAt).ToList()
    );

    private static ContributionResponse MapContribution(Contribution c) => new(
        c.Id, c.ContributorName, c.Message, c.Amount, c.CreatedAt
    );

    // ── Creator: list my registries ──────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetMyRegistries()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        var registries = await _db.Registries
            .Where(r => r.UserId == userId)
            .Include(r => r.User)
            .Include(r => r.GiftGoals)
                .ThenInclude(g => g.Contributions)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(registries.Select(MapRegistry));
    }

    // ── Creator: create a registry ───────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CreateRegistry([FromBody] CreateRegistryRequest req)
    {
        var userId = await GetVerifiedUserIdAsync();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Registry name is required." });

        var registry = new Registry
        {
            Name = req.Name.Trim(),
            Description = req.Description?.Trim() ?? string.Empty,
            Slug = GenerateSlug(req.Name),
            UserId = userId.Value
        };

        _db.Registries.Add(registry);
        await _db.SaveChangesAsync();

        // Reload with includes
        var created = await _db.Registries
            .Include(r => r.User)
            .Include(r => r.GiftGoals).ThenInclude(g => g.Contributions)
            .FirstAsync(r => r.Id == registry.Id);

        return CreatedAtAction(nameof(GetBySlug), new { slug = registry.Slug }, MapRegistry(created));
    }

    // ── Public: get registry by slug ─────────────────────────────────────────

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var registry = await _db.Registries
            .Include(r => r.User)
            .Include(r => r.GiftGoals)
                .ThenInclude(g => g.Contributions)
            .FirstOrDefaultAsync(r => r.Slug == slug);

        if (registry == null) return NotFound(new { error = "Registry not found." });

        return Ok(MapRegistry(registry));
    }

    // ── Creator: add a gift goal ─────────────────────────────────────────────

    [HttpPost("{id:int}/goals")]
    public async Task<IActionResult> AddGoal(int id, [FromBody] CreateGiftGoalRequest req)
    {
        var userId = await GetVerifiedUserIdAsync();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        var registry = await _db.Registries.FirstOrDefaultAsync(r => r.Id == id);
        if (registry == null) return NotFound(new { error = "Registry not found." });
        if (registry.UserId != userId) return Forbid();

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Goal name is required." });
        if (req.TargetAmount <= 0)
            return BadRequest(new { error = "Target amount must be greater than zero." });

        var goal = new GiftGoal
        {
            Name = req.Name.Trim(),
            Description = req.Description?.Trim() ?? string.Empty,
            TargetAmount = req.TargetAmount,
            ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim(),
            ProductLink = string.IsNullOrWhiteSpace(req.ProductLink) ? null : req.ProductLink.Trim(),
            RegistryId = id
        };

        _db.GiftGoals.Add(goal);
        await _db.SaveChangesAsync();

        goal.Contributions = new List<Contribution>();
        return Ok(MapGoal(goal));
    }

    // ── Creator: update registry name / description ──────────────────────────

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateRegistry(int id, [FromBody] UpdateRegistryRequest req)
    {
        var userId = await GetVerifiedUserIdAsync();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        var registry = await _db.Registries
            .Include(r => r.User)
            .Include(r => r.GiftGoals).ThenInclude(g => g.Contributions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (registry == null) return NotFound(new { error = "Registry not found." });
        if (registry.UserId != userId) return Forbid();

        if (req.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Registry name cannot be empty." });
            registry.Name = req.Name.Trim();
        }

        if (req.Description is not null)
            registry.Description = req.Description.Trim();

        if (req.HeroBackgroundColor is not null)
            registry.HeroBackgroundColor = string.IsNullOrWhiteSpace(req.HeroBackgroundColor)
                ? null : req.HeroBackgroundColor.Trim();

        if (req.HeroImageUrl is not null)
            registry.HeroImageUrl = string.IsNullOrWhiteSpace(req.HeroImageUrl)
                ? null : req.HeroImageUrl.Trim();

        await _db.SaveChangesAsync();
        return Ok(MapRegistry(registry));
    }

    // ── Creator: upload hero background image ────────────────────────────────

    [HttpPost("{id:int}/upload-hero")]
    public async Task<IActionResult> UploadHeroImage(int id, IFormFile image)
    {
        var userId = await GetVerifiedUserIdAsync();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        var registry = await _db.Registries
            .Include(r => r.User)
            .Include(r => r.GiftGoals).ThenInclude(g => g.Contributions)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (registry == null) return NotFound(new { error = "Registry not found." });
        if (registry.UserId != userId) return Forbid();

        if (image == null || image.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowed.Contains(image.ContentType.ToLower()))
            return BadRequest(new { error = "Only JPEG, PNG, or WebP images are allowed." });

        if (image.Length > 8 * 1024 * 1024)
            return BadRequest(new { error = "Image must be smaller than 8 MB." });

        var ext = Path.GetExtension(image.FileName).ToLower();
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"hero-{id}-{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = System.IO.File.Create(filePath);
        await image.CopyToAsync(stream);

        registry.HeroImageUrl = $"/uploads/{fileName}";
        await _db.SaveChangesAsync();

        return Ok(MapRegistry(registry));
    }

    // ── Creator: update a gift goal's fields ─────────────────────────────────

    [HttpPatch("{id:int}/goals/{goalId:int}")]
    public async Task<IActionResult> UpdateGoal(int id, int goalId, [FromBody] UpdateGiftGoalRequest req)
    {
        var userId = await GetVerifiedUserIdAsync();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        var registry = await _db.Registries.FirstOrDefaultAsync(r => r.Id == id);
        if (registry == null) return NotFound(new { error = "Registry not found." });
        if (registry.UserId != userId) return Forbid();

        var goal = await _db.GiftGoals
            .Include(g => g.Contributions)
            .FirstOrDefaultAsync(g => g.Id == goalId && g.RegistryId == id);
        if (goal == null) return NotFound(new { error = "Gift goal not found." });

        if (req.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Goal name cannot be empty." });
            goal.Name = req.Name.Trim();
        }

        if (req.Description is not null)
            goal.Description = req.Description.Trim();

        if (req.TargetAmount is not null)
        {
            if (req.TargetAmount <= 0)
                return BadRequest(new { error = "Target amount must be greater than zero." });
            goal.TargetAmount = req.TargetAmount.Value;
        }

        if (req.ProductLink is not null)
            goal.ProductLink = string.IsNullOrWhiteSpace(req.ProductLink) ? null : req.ProductLink.Trim();

        await _db.SaveChangesAsync();
        return Ok(MapGoal(goal));
    }

    // ── Creator: upload image for a gift goal ────────────────────────────────

    [HttpPost("{id:int}/goals/{goalId:int}/upload-image")]
    public async Task<IActionResult> UploadGoalImage(int id, int goalId, IFormFile image)
    {
        var userId = await GetVerifiedUserIdAsync();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        var registry = await _db.Registries.FirstOrDefaultAsync(r => r.Id == id);
        if (registry == null) return NotFound(new { error = "Registry not found." });
        if (registry.UserId != userId) return Forbid();

        var goal = await _db.GiftGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.RegistryId == id);
        if (goal == null) return NotFound(new { error = "Gift goal not found." });

        if (image == null || image.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowed.Contains(image.ContentType.ToLower()))
            return BadRequest(new { error = "Only JPEG, PNG, WebP, or GIF images are allowed." });

        if (image.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "Image must be smaller than 5 MB." });

        var ext = Path.GetExtension(image.FileName).ToLower();
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"goal-{goalId}-{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = System.IO.File.Create(filePath);
        await image.CopyToAsync(stream);

        // Persist the new image URL on the goal
        goal.ImageUrl = $"/uploads/{fileName}";
        await _db.SaveChangesAsync();

        return Ok(new { url = goal.ImageUrl });
    }

    // ── Creator: delete a gift goal ──────────────────────────────────────────

    [HttpDelete("{id:int}/goals/{goalId:int}")]
    public async Task<IActionResult> DeleteGoal(int id, int goalId)
    {
        var userId = await GetVerifiedUserIdAsync();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        var registry = await _db.Registries.FirstOrDefaultAsync(r => r.Id == id);
        if (registry == null) return NotFound(new { error = "Registry not found." });
        if (registry.UserId != userId) return Forbid();

        var goal = await _db.GiftGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.RegistryId == id);
        if (goal == null) return NotFound(new { error = "Goal not found." });

        _db.GiftGoals.Remove(goal);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Creator: delete a registry ───────────────────────────────────────────

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRegistry(int id)
    {
        var userId = await GetVerifiedUserIdAsync();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        var registry = await _db.Registries.FirstOrDefaultAsync(r => r.Id == id);
        if (registry == null) return NotFound(new { error = "Registry not found." });
        if (registry.UserId != userId) return Forbid();

        _db.Registries.Remove(registry);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
