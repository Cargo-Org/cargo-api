using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Notifications.Email;
using Cargo.BuildingBlocks.Security.Keycloak;
using Cargo.BuildingBlocks.Utils.OTP;
using Cargo.DriverService.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Auth.ForgotPassword;

public sealed class ForgotPasswordCommandHandler(
    DriverDbContext dbContext,
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
        var profile = await dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Email == command.Email, cancellationToken);

        if (profile is null)
        {
            logger.LogInformation(
                "Forgot-password requested for unknown email {Email} — silently succeeding.",
                command.Email);
            return Unit.Value;
        }

        var keycloakUserId = await keycloakAdminClient
            .GetUserIdByEmailAsync(command.Email, cancellationToken);

        if (keycloakUserId is null)
        {
            logger.LogWarning(
                "Forgot-password: local profile exists for {Email} but Keycloak user not found.",
                command.Email);
            return Unit.Value;
        }

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
            logger.LogError(ex,
                "Failed to send password reset OTP for {Email}", command.Email);
        }

        return Unit.Value;
    }
}
