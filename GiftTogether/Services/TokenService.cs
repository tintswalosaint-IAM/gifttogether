using System.Security.Cryptography;
using System.Text;

namespace GiftTogether.Services;

/// <summary>
/// Simple HMAC-based token service for MVP auth.
/// Token format: base64(userId:issuedAtUnixSeconds):base64(hmac)
/// Tokens expire after <see cref="TokenLifetimeDays"/> days of inactivity.
/// </summary>
public class TokenService
{
    /// <summary>How long a token stays valid. 90 days matches "stay logged in" UX.</summary>
    private const int TokenLifetimeDays = 90;

    private readonly string _secret;

    public TokenService(IConfiguration config)
    {
        _secret = config["Auth:Secret"] ?? "gifttogether-dev-secret-change-in-production";
    }

    public string GenerateToken(int userId)
    {
        var payload = $"{userId}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        var sig = ComputeHmac(payloadB64);
        return $"{payloadB64}.{sig}";
    }

    /// <summary>
    /// Validates the token signature and checks it has not expired.
    /// Returns the userId on success, null on any failure.
    /// </summary>
    public int? ValidateToken(string token)
    {
        try
        {
            // Split on the first '.' only — payload and sig are both base64
            // which cannot contain '.', so exactly 2 parts is always correct.
            var parts = token.Split('.', 2);
            if (parts.Length != 2) return null;

            // 1. Verify HMAC signature (constant-time compare prevents timing attacks)
            var expectedSig = ComputeHmac(parts[0]);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedSig);
            var actualBytes   = Encoding.UTF8.GetBytes(parts[1]);

            // Lengths must match before FixedTimeEquals (it throws on mismatch)
            if (expectedBytes.Length != actualBytes.Length) return null;
            if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
                return null;

            // 2. Decode payload: "userId:issuedAtUnixSeconds"
            var payload  = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
            var segments = payload.Split(':', 2);
            if (segments.Length != 2) return null;

            if (!int.TryParse(segments[0], out var userId)) return null;
            if (!long.TryParse(segments[1], out var issuedAtSeconds)) return null;

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds);

            // 3. Check expiry
            if (DateTimeOffset.UtcNow - issuedAt > TimeSpan.FromDays(TokenLifetimeDays))
                return null;

            return userId;
        }
        catch
        {
            return null;
        }
    }

    private string ComputeHmac(string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToBase64String(hmac.ComputeHash(dataBytes));
    }
}
