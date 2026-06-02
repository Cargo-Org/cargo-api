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
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to authenticate Keycloak user for {Email}", command.Email);
            return Error.Failure(
                code: "Login.Failed",
                description: "Authentication failed. Please try again.");
        }

        // ── Step 2: Load local profile ──────────────────────────────────
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

        // ── Step 3: Check Phone Verification ────────────────────────────
        if (!customer.IsPhoneVerified)
        {
            logger.LogWarning(
                "Login blocked for {Email} — phone not verified. Resending OTP.",
                command.Email);

            if (!string.IsNullOrWhiteSpace(customer.PhoneNumber))
            {
                await ResendPhoneVerificationOtpAsync(customer.PhoneNumber, cancellationToken);
            }

            return Error.Unauthorized(
                code: "Login.PhoneNotVerified",
                description: "Your phone number has not been verified. " +
                             "A new verification code has been sent to your WhatsApp.");
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