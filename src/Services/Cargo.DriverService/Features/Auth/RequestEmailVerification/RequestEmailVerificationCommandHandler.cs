using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Messaging;
using Cargo.BuildingBlocks.Notifications.Email;
using Cargo.BuildingBlocks.Utils.OTP;
using Cargo.DriverService.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Auth.RequestEmailVerification;

public sealed class RequestEmailVerificationCommandHandler(
    DriverDbContext dbContext,
    IOtpService otpService,
    INotificationPublisher notificationPublisher,
    ILogger<RequestEmailVerificationCommandHandler> logger)
    : ICommandHandler<RequestEmailVerificationCommand, MediatR.Unit>
{
    public async Task<ErrorOr<MediatR.Unit>> Handle(
        RequestEmailVerificationCommand command,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.KeycloakUserId == command.KeycloakUserId, cancellationToken);

        if (profile is null)
        {
            return Error.NotFound(
                code: "Profile.NotFound",
                description: "Driver profile not found.");
        }

        if (profile.IsEmailVerified)
        {
            return Error.Conflict(
                code: "VerifyEmail.AlreadyVerified",
                description: "Your email address is already verified.");
        }

        try
        {
            var otp = await otpService.GenerateAsync(
                profile.Email, OtpPurpose.EmailVerification, cancellationToken);

            await notificationPublisher.PublishAsync(
                NotificationMessage.EmailOtp(
                    profile.Email,
                    profile.FullName,
                    otp,
                    OtpEmailType.EmailVerification),
                cancellationToken);

            logger.LogInformation("Sent optional email verification OTP for {Email}", profile.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email verification OTP for {Email}", profile.Email);
            return Error.Failure(
                code: "VerifyEmail.FailedToSend",
                description: "Failed to send verification email. Please try again later.");
        }

        return MediatR.Unit.Value;
    }
}
