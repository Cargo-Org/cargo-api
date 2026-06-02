using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Exceptions;
using Cargo.BuildingBlocks.Messaging;
using Cargo.BuildingBlocks.Notifications.Email;
using Cargo.BuildingBlocks.Security.Keycloak;
using Cargo.BuildingBlocks.Utils.OTP;
using Cargo.CustomerService.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace Cargo.CustomerService.Features.Auth.Login;

public sealed class LoginCommandHandler(
    CustomerDbContext dbContext,
    IOtpService otpService,
    INotificationPublisher notificationPublisher,
    IKeycloakAdminClient keycloakAdminClient,
    ILogger<LoginCommandHandler> logger)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    private static readonly JwtSecurityTokenHandler _jwtHandler = new();

    public async Task<ErrorOr<LoginResponse>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Authenticate via Keycloak ROPC ──────────────────────
        AuthTokenResponse userTokenResponse;
        try
        {
            userTokenResponse = await keycloakAdminClient.LoginAsync(
                command.Email,
                command.Password,
                cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Error.Unauthorized(
                code: "Login.InvalidCredentials",
                description: "Invalid email or password.");
        }
        catch (EmailNotVerifiedException)
        {
            // Keycloak returned 400 "Account is not fully set up" —
            // the user hasn't verified their email yet.
            logger.LogWarning(
                "Login blocked for {Email} — Keycloak reports email not verified. Resending OTP.",
                command.Email);

            await ResendVerificationOtpAsync(command.Email, cancellationToken);

            return Error.Unauthorized(
                code: "Login.EmailNotVerified",
                description: "Your email address has not been verified. " +
                             "A new verification code has been sent to your inbox.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to authenticate Keycloak user for {Email}", command.Email);
            return Error.Failure(
                code: "Login.Failed",
                description: "Authentication failed. Please try again.");
        }

        // ── Step 2: Check email_verified claim inside the JWT ───────────
        // Keycloak ROPC always returns 200 regardless of emailVerified.
        // We must inspect the token ourselves — this is a standard OIDC
        // claim, always present in Keycloak-issued JWTs.
        if (!IsEmailVerified(userTokenResponse.AccessToken))
        {
            logger.LogWarning(
                "Login blocked for {Email} — email not verified. Resending OTP.",
                command.Email);

            // Re-send OTP so the user can verify without a separate request
            await ResendVerificationOtpAsync(command.Email, cancellationToken);

            return Error.Unauthorized(
                code: "Login.EmailNotVerified",
                description: "Your email address has not been verified. " +
                             "A new verification code has been sent to your inbox.");
        }

        // ── Step 3: Load local profile ──────────────────────────────────
        var customer = await dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email == command.Email, cancellationToken);

        if (customer is null)
        {
            logger.LogWarning(
                "User {Email} authenticated in Keycloak but no local profile found.",
                command.Email);

            return Error.NotFound(
                code: "Login.LocalProfileNotFound",
                description: "User profile not found.");
        }

        // ── Step 4: Return tokens ───────────────────────────────────────
        return new LoginResponse(
            AccessToken: userTokenResponse.AccessToken,
            AccessTokenExpiresIn: userTokenResponse.ExpiresIn,
            RefreshToken: userTokenResponse.RefreshToken,
            RefreshTokenExpiresIn: userTokenResponse.RefreshExpiresIn,
            CustomerId: customer.Id,
            FullName: customer.FullName
        );
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Decodes the JWT (no signature verification needed — we just got it
    /// directly from Keycloak over an internal network call) and reads the
    /// standard OIDC <c>email_verified</c> boolean claim.
    /// </summary>
    private static bool IsEmailVerified(string accessToken)
    {
        try
        {
            // ReadJwtToken does NOT validate signature — intentional here.
            // We trust the token because we just received it from Keycloak
            // in the same request. Signature is validated by the JWT middleware
            // on every subsequent protected endpoint call.
            var jwt = _jwtHandler.ReadJwtToken(accessToken);
            var claim = jwt.Claims.FirstOrDefault(c => c.Type == "email_verified");

            return claim is not null &&
                   bool.TryParse(claim.Value, out bool verified) &&
                   verified;
        }
        catch
        {
            // Malformed token — treat as unverified to be safe
            return false;
        }
    }

    /// <summary>
    /// Generates a fresh OTP and enqueues it via the outbox publisher.
    /// OtpService.GenerateAsync handles caching internally.
    /// </summary>
    private async Task ResendVerificationOtpAsync(
        string email, CancellationToken ct)
    {
        try
        {
            // Look up the customer's name for a personalised email.
            var customer = await dbContext.CustomerProfiles
                .AsNoTracking()
                .Where(c => c.Email == email)
                .Select(c => c.FullName)
                .FirstOrDefaultAsync(ct);

            var displayName = string.IsNullOrWhiteSpace(customer) ? "there" : customer;

            // GenerateAsync already stores the hashed OTP in Redis.
            // The return value is the plain-text code for the email only.
            var otp = await otpService.GenerateAsync(
                email, OtpPurpose.EmailVerification, ct);

            await notificationPublisher.PublishAsync(
                NotificationMessage.EmailOtp(
                    email, displayName, otp, OtpEmailType.EmailVerification),
                ct);
        }
        catch (Exception ex)
        {
            // Non-critical path — log but don't fail the login response.
            // The user will see "EmailNotVerified" and can retry.
            logger.LogError(ex,
                "Failed to resend verification OTP for {Email}", email);
        }
    }
}