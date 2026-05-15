using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Security.Keycloak;
using Cargo.BuildingBlocks.Utils.OTP;
using ErrorOr;
using MediatR;

namespace Cargo.CustomerService.Features.Auth.ResetPassword;

public sealed class ResetPasswordCommandHandler(
    IOtpService otpService,
    IKeycloakAdminClient keycloakAdminClient,
    ILogger<ResetPasswordCommandHandler> logger)
    : ICommandHandler<ResetPasswordCommand, Unit>
{
    public async Task<ErrorOr<Unit>> Handle(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Validate OTP ─────────────────────────────────────────
        var isValid = await otpService.ValidateAsync(
            command.Email, OtpPurpose.PasswordReset,
            command.OtpCode, cancellationToken);

        if (!isValid)
        {
            return Error.Validation(
                code: "ResetPassword.InvalidOtpCode",
                description: "Invalid or expired reset code.");
        }

        // ── Step 2: Find user in Keycloak ────────────────────────────────
        var userId = await keycloakAdminClient
            .GetUserIdByEmailAsync(command.Email, cancellationToken);

        if (userId is null)
        {
            return Error.NotFound(
                code: "ResetPassword.UserNotFound",
                description: "User not found.");
        }

        // ── Step 3: Reset password in Keycloak ──────────────────────────
        try
        {
            await keycloakAdminClient.ResetPasswordAsync(
                userId, command.NewPassword, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to reset password for user {Email}", command.Email);
            return Error.Failure(
                code: "ResetPassword.Failed",
                description: "Failed to reset password. Please try again.");
        }

        // ── Step 4: Invalidate OTP ──────────────────────────────────────
        await otpService.InvalidateAsync(
            command.Email, OtpPurpose.PasswordReset, cancellationToken);

        logger.LogInformation(
            "Password successfully reset for {Email}", command.Email);

        return Unit.Value;
    }
}
