namespace Cargo.BuildingBlocks.Utils.OTP;

/// <summary>
/// Generates and validates cryptographically-secure 5-digit OTP codes.
/// Codes are stored in Redis under a keyed namespace with a fixed TTL.
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates a new OTP, stores it in cache under
    /// <c>otp:{purpose}:{identifier}</c>, and returns the plain-text code
    /// so the caller can embed it in an email.
    /// </summary>
    /// <param name="identifier">Typically the user's email address.</param>
    /// <param name="purpose">
    ///   Namespaces the key so email-verify and password-reset codes
    ///   cannot be cross-used. Use <see cref="OtpPurpose"/> constants.
    /// </param>
    Task<string> GenerateAsync(string identifier, string purpose,
                               CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> if the supplied <paramref name="code"/> matches
    /// the cached value and has not expired.  The cache entry is NOT
    /// deleted here; call <see cref="InvalidateAsync"/> after success.
    /// </summary>
    Task<bool> ValidateAsync(string identifier, string purpose, string code,
                             CancellationToken ct = default);

    /// <summary>
    /// Removes the OTP cache entry regardless of TTL.
    /// Call this after a successful validation to prevent replay.
    /// </summary>
    Task InvalidateAsync(string identifier, string purpose,
                         CancellationToken ct = default);
}

/// <summary>
/// Well-known OTP purpose strings used as Redis key namespaces.
/// </summary>
public static class OtpPurpose
{
    public const string EmailVerification = "email-verify";
    public const string PhoneVerification = "phone-verify";
    public const string PasswordReset = "pwd-reset";
}
