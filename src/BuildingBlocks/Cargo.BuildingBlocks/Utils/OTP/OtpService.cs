using Cargo.BuildingBlocks.Utils.Cache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Cargo.BuildingBlocks.Utils.OTP;

/// <summary>
/// Generates cryptographically-secure OTP codes using
/// <see cref="RandomNumberGenerator"/> (not System.Random).
///
/// Cache key schema:  otp:{purpose}:{identifier}
///   e.g.  otp:email-verify:user@example.com
///         otp:pwd-reset:user@example.com
///
/// The cache stores only the HMAC-SHA256 hash of the code, never
/// the plain-text value, so even a full Redis dump can't be replayed
/// without brute-forcing the 6-digit space (max 10^6 combos).
/// </summary>

public class OtpService(
    ICacheService cache,
    IOptions<OtpSettings> options,
    ILogger<OtpService> logger) : IOtpService
{
    private readonly OtpSettings _settings = options.Value;

    // ──────────────────────────────────────────────────────────
    //  Key helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the Redis key.
    /// We normalise the identifier to lowercase to be case-insensitive.
    /// </summary>
    private static string BuildKey(string identifier, string purpose) =>
        $"otp:{purpose}:{identifier.ToLowerInvariant()}";

    // ──────────────────────────────────────────────────────────
    //  GenerateAsync
    // ──────────────────────────────────────────────────────────

    public async Task<string> GenerateAsync(
        string identifier, string purpose, CancellationToken ct = default)
    {
        // 1. Generate a cryptographically-random N-digit code
        int ceiling = (int)Math.Pow(10, _settings.CodeLength); // 100_000 for 5 digits
        int raw = RandomNumberGenerator.GetInt32(0, ceiling);
        string code = raw.ToString($"D{_settings.CodeLength}");  // zero-padded: "00734"

        // 2. Hash the plain-text code before storing
        string hash = HashCode(code);

        // 3. Store hash in Redis with the configured TTL
        string key = BuildKey(identifier, purpose);
        await cache.SetAsync(key, hash, TimeSpan.FromMinutes(_settings.TtlMinutes), ct);

        logger.LogInformation(
            "OTP generated for {Identifier} [{Purpose}] — expires in {Ttl} min",
            MaskEmail(identifier), purpose, _settings.TtlMinutes);

        // 4. Return plain-text code to the caller (who will email it)
        return code;
    }

    // ──────────────────────────────────────────────────────────
    //  ValidateAsync
    // ──────────────────────────────────────────────────────────

    public async Task<bool> ValidateAsync(
        string identifier, string purpose, string code, CancellationToken ct = default)
    {
        string key = BuildKey(identifier, purpose);
        string? storedHash = await cache.GetAsync<string>(key, ct);

        if (storedHash is null)
        {
            logger.LogWarning(
                "OTP validation failed — key not found: {Identifier} [{Purpose}]",
                MaskEmail(identifier), purpose);
            return false;
        }

        // Constant-time comparison to prevent timing attacks
        bool valid = CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(HashCode(code)),
            System.Text.Encoding.UTF8.GetBytes(storedHash));

        if (!valid)
        {
            logger.LogWarning(
                "OTP validation failed — wrong code for {Identifier} [{Purpose}]",
                MaskEmail(identifier), purpose);
        }

        return valid;
    }

    // ──────────────────────────────────────────────────────────
    //  InvalidateAsync
    // ──────────────────────────────────────────────────────────

    public async Task InvalidateAsync(
        string identifier, string purpose, CancellationToken ct = default)
    {
        string key = BuildKey(identifier, purpose);
        await cache.RemoveAsync(key, ct);

        logger.LogInformation(
            "OTP invalidated for {Identifier} [{Purpose}]",
            MaskEmail(identifier), purpose);
    }

    // ──────────────────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// SHA-256 hash of the raw OTP string.
    /// Stored as lowercase hex. 64-char string.
    /// </summary>
    private static string HashCode(string code)
    {
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Masks an email for log output: ze***@gmail.com</summary>
    private static string MaskEmail(string email)
    {
        int at = email.IndexOf('@');
        if (at <= 2) return "***";
        return string.Concat(email.AsSpan(0, 2), "***", email.AsSpan(at));
    }
}