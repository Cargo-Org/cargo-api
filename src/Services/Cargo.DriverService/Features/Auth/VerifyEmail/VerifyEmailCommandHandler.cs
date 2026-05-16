using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Security.Keycloak;
using Cargo.BuildingBlocks.Utils.OTP;
using ErrorOr;
using MediatR;

namespace Cargo.DriverService.Features.Auth.VerifyEmail;

public sealed class VerifyEmailCommandHandler(
    IOtpService otpService,
    IKeycloakAdminClient keycloakAdminClient)
    : ICommandHandler<VerifyEmailCommand, Unit>
{
    public async Task<ErrorOr<Unit>> Handle(
        VerifyEmailCommand command,
        CancellationToken cancellationToken)
    {
        var isValid = await otpService.ValidateAsync(command.Email, OtpPurpose.EmailVerification, command.OtpCode, cancellationToken);

        if (!isValid)
        {
            return Error.Validation(
                code: "VerifyEmail.InvalidOtpCode",
                description: "Invalid or expired verification code.");
        }

        var userId = await keycloakAdminClient.GetUserIdByEmailAsync(command.Email, cancellationToken);

        if (userId is null)
        {
            return Error.NotFound(
                code: "VerifyEmail.UserNotFound",
                description: "Identity sync error. User not found.");
        }

        await keycloakAdminClient.UpdateUserEmailVerifiedAsync(userId, true, cancellationToken);

        await otpService.InvalidateAsync(command.Email, OtpPurpose.EmailVerification, cancellationToken);

        return Unit.Value;
    }
}
