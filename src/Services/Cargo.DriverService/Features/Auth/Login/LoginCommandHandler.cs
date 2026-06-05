using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Messaging;
using Cargo.BuildingBlocks.Security.Keycloak;
using Cargo.BuildingBlocks.Utils.OTP;
using Cargo.DriverService.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Auth.Login;

public sealed class LoginCommandHandler(
    DriverDbContext dbContext,
    IOtpService otpService,
    INotificationPublisher notificationPublisher,
    IKeycloakAdminClient keycloakAdminClient,
    ILogger<LoginCommandHandler> logger)
    : ICommandHandler<LoginCommand, LoginResponse>
{
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
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to authenticate Keycloak user for {Email}", command.Email);
            return Error.Failure(
                code: "Login.Failed",
                description: "Authentication failed. Please try again.");
        }

        // ── Step 2: Load local profile ──────────────────────────────────
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

        // ── Step 3: Check Phone Verification ────────────────────────────
        if (!driver.IsPhoneVerified)
        {
            logger.LogWarning(
                "Login blocked for {Email} — phone not verified. Resending OTP.",
                command.Email);

            var otpSent = false;
            if (!string.IsNullOrWhiteSpace(driver.PhoneNumber))
            {
                await ResendPhoneVerificationOtpAsync(driver.PhoneNumber, cancellationToken);
                otpSent = true;
            }

            var description = otpSent
                ? "Your phone number has not been verified. A new verification code has been sent to your WhatsApp."
                : "Your phone number has not been verified. Please complete your profile to receive a verification code.";

            return Error.Unauthorized(
                code: "Login.PhoneNotVerified",
                description: description);
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

    private async Task ResendPhoneVerificationOtpAsync(
        string phoneNumber, CancellationToken ct)
    {
        try
        {
            var otp = await otpService.GenerateAsync(
                phoneNumber, OtpPurpose.PhoneVerification, ct);

            await notificationPublisher.PublishAsync(
                NotificationMessage.WhatsApp(
                    phoneNumber,
                    $"Your Cargo verification code is {otp}. Do not share this code with anyone."),
                ct);
        }
        catch (Exception ex)
        {
            // Non-critical path — log but don't fail the login response.
            logger.LogError(ex,
                "Failed to resend verification OTP for {PhoneNumber}", phoneNumber);
        }
    }
}
