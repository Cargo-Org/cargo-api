using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Notifications.Email;
using Cargo.BuildingBlocks.Security.Keycloak;
using Cargo.BuildingBlocks.Utils.OTP;
using Cargo.CustomerService.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Auth.ForgotPassword;

public sealed class ForgotPasswordCommandHandler(
    CustomerDbContext dbContext,
    IKeycloakAdminClient keycloakAdminClient,
    IOtpService otpService,
    IEmailService emailService,
    ILogger<ForgotPasswordCommandHandler> logger)
    : ICommandHandler<ForgotPasswordCommand, Unit>
{
    public async Task<ErrorOr<Unit>> Handle(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken)
    {
        // ── Security: Always return 200 regardless of whether the email exists.
        // This prevents email enumeration attacks.

        // Look up the local profile to get the display name for the email.
        var profile = await dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Email == command.Email, cancellationToken);

        if (profile is null)
        {
            // Email not in our system — silently succeed to prevent enumeration.
            logger.LogInformation(
                "Forgot-password requested for unknown email {Email} — silently succeeding.",
                command.Email);
            return Unit.Value;
        }

        // Verify the user actually exists in Keycloak too.
        var keycloakUserId = await keycloakAdminClient
            .GetUserIdByEmailAsync(command.Email, cancellationToken);

        if (keycloakUserId is null)
        {
            logger.LogWarning(
                "Forgot-password: local profile exists for {Email} but Keycloak user not found.",
                command.Email);
            return Unit.Value;
        }

        // Generate and send OTP.
        try
        {
            var otp = await otpService.GenerateAsync(
                command.Email, OtpPurpose.PasswordReset, cancellationToken);

            var displayName = profile.FullName ?? "there";

            await emailService.SendOtpAsync(
                command.Email, displayName, otp,
                OtpEmailType.PasswordReset, cancellationToken);

            logger.LogInformation(
                "Password reset OTP sent to {Email}", command.Email);
        }
        catch (Exception ex)
        {
            // Log but still return success — don't reveal failure to the client.
            logger.LogError(ex,
                "Failed to send password reset OTP for {Email}", command.Email);
        }

        return Unit.Value;
    }
}
