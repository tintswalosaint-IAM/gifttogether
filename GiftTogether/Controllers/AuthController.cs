using GiftTogether.Data;
using GiftTogether.DTOs;
using GiftTogether.Models;
using GiftTogether.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GiftTogether.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    private readonly IWebHostEnvironment _env;

    public AuthController(AppDbContext db, TokenService tokens, IWebHostEnvironment env)
    {
        _db = db;
        _tokens = tokens;
        _env = env;
    }

    private int? GetCurrentUserId()
    {
        var auth = Request.Headers.Authorization.FirstOrDefault();
        if (auth == null || !auth.StartsWith("Bearer ")) return null;
        return _tokens.ValidateToken(auth["Bearer ".Length..]);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) ||
            string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Name, email, and password are required." });

        if (string.IsNullOrWhiteSpace(req.GuestMessage))
            return BadRequest(new { error = "A message to your guests is required." });

        if (req.Password.Length < 6)
            return BadRequest(new { error = "Password must be at least 6 characters." });

        var exists = await _db.Users.AnyAsync(u => u.Email == req.Email.ToLower());
        if (exists)
            return Conflict(new { error = "An account with that email already exists." });

        var user = new User
        {
            Name = req.Name.Trim(),
            Email = req.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            ProfileImageUrl = string.IsNullOrWhiteSpace(req.ProfileImageUrl) ? null : req.ProfileImageUrl.Trim(),
            GuestMessage = req.GuestMessage.Trim()
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokens.GenerateToken(user.Id);
        return Ok(new AuthResponse(user.Id, user.Name, user.Email, token));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Email and password are required." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLower());
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password." });

        var token = _tokens.GenerateToken(user.Id);
        return Ok(new AuthResponse(user.Id, user.Name, user.Email, token));
    }

    /// <summary>
    /// Update the creator's profile image and/or guest message.
    /// </summary>
    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound(new { error = "User not found." });

        // GuestMessage is required — reject an explicit empty string
        if (req.GuestMessage is not null && string.IsNullOrWhiteSpace(req.GuestMessage))
            return BadRequest(new { error = "A message to your guests is required." });

        if (req.ProfileImageUrl is not null)
            user.ProfileImageUrl = string.IsNullOrWhiteSpace(req.ProfileImageUrl) ? null : req.ProfileImageUrl.Trim();

        if (req.GuestMessage is not null)
            user.GuestMessage = req.GuestMessage.Trim();

        await _db.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.Name,
            user.Email,
            user.ProfileImageUrl,
            user.GuestMessage
        });
    }

    /// <summary>
    /// Upload a profile photo. Accepts multipart/form-data with a single file field named "photo".
    /// Saves to wwwroot/uploads/ and returns the public URL.
    /// </summary>
    [HttpPost("upload-photo")]
    public async Task<IActionResult> UploadPhoto(IFormFile photo)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        if (photo == null || photo.Length == 0)
            return BadRequest(new { error = "No file provided." });

        // Allow only common image types
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowed.Contains(photo.ContentType.ToLower()))
            return BadRequest(new { error = "Only JPEG, PNG, WebP, or GIF images are allowed." });

        // 5 MB limit
        if (photo.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "Image must be smaller than 5 MB." });

        var ext = Path.GetExtension(photo.FileName).ToLower();
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);

        // Use userId + guid so each upload gets a unique, non-guessable name
        var fileName = $"profile-{userId}-{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = System.IO.File.Create(filePath);
        await photo.CopyToAsync(stream);

        return Ok(new { url = $"/uploads/{fileName}" });
    }

    /// <summary>
    /// Get the current user's profile (for pre-filling the profile form).
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { error = "Authentication required." });

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound(new { error = "User not found." });

        return Ok(new
        {
            user.Id,
            user.Name,
            user.Email,
            user.ProfileImageUrl,
            user.GuestMessage
        });
    }
}
