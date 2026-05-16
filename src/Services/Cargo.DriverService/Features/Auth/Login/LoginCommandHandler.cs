using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Exceptions;
using Cargo.BuildingBlocks.Notifications.Email;
using Cargo.BuildingBlocks.Security.Keycloak;
using Cargo.BuildingBlocks.Utils.OTP;
using Cargo.DriverService.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace Cargo.DriverService.Features.Auth.Login;

public sealed class LoginCommandHandler(
    DriverDbContext dbContext,
    IOtpService otpService,
    IEmailService emailService,
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
        if (!IsEmailVerified(userTokenResponse.AccessToken))
        {
            logger.LogWarning(
                "Login blocked for {Email} — email not verified. Resending OTP.",
                command.Email);

            await ResendVerificationOtpAsync(command.Email, cancellationToken);

            return Error.Unauthorized(
                code: "Login.EmailNotVerified",
                description: "Your email address has not been verified. " +
                             "A new verification code has been sent to your inbox.");
        }

        // ── Step 3: Load local profile ──────────────────────────────────
        var driver = await dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email == command.Email, cancellationToken);

        if (driver is null)
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
            DriverId: driver.Id,
            FullName: driver.FullName
        );
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static bool IsEmailVerified(string accessToken)
    {
        try
        {
            var jwt = _jwtHandler.ReadJwtToken(accessToken);
            var claim = jwt.Claims.FirstOrDefault(c => c.Type == "email_verified");

            return claim is not null &&
                   bool.TryParse(claim.Value, out bool verified) &&
                   verified;
        }
        catch
        {
            return false;
        }
    }

    private async Task ResendVerificationOtpAsync(
        string email, CancellationToken ct)
    {
        try
        {
            var driver = await dbContext.DriverProfiles
                .AsNoTracking()
                .Where(c => c.Email == email)
                .Select(c => c.FullName)
                .FirstOrDefaultAsync(ct);

            var displayName = string.IsNullOrWhiteSpace(driver) ? "there" : driver;

            var otp = await otpService.GenerateAsync(
                email, OtpPurpose.EmailVerification, ct);

            await emailService.SendOtpAsync(
                email, displayName, otp, OtpEmailType.EmailVerification, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to resend verification OTP for {Email}", email);
        }
    }
}
