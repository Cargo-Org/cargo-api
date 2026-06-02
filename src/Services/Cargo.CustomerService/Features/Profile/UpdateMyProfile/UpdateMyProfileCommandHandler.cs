using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Data;
using Cargo.CustomerService.Domain.Enums;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

using Cargo.BuildingBlocks.Messaging;
using Cargo.BuildingBlocks.Utils.OTP;

namespace Cargo.CustomerService.Features.Profile.UpdateMyProfile;

public sealed class UpdateMyProfileCommandHandler(
    CustomerDbContext dbContext,
    IOtpService otpService,
    INotificationPublisher notificationPublisher,
    ILogger<UpdateMyProfileCommandHandler> logger)
    : ICommandHandler<UpdateMyProfileCommand, ProfileResponse>
{
    public async Task<ErrorOr<ProfileResponse>> Handle(
        UpdateMyProfileCommand command,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.CustomerProfiles
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(
                p => p.KeycloakUserId == command.KeycloakUserId,
                cancellationToken);

        if (profile is null)
            return Error.NotFound(
                code: "Profile.NotFound",
                description: "Customer profile not found.");

        // UpdateProfile calls RecomputeOnboardingStatus internally.
        // OnboardingStatus transitions from MissingProfileData → MissingFiles
        // if both fields are now populated.
        profile.UpdateProfile(command.FirstName, command.LastName, command.PhoneNumber);

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!profile.IsPhoneVerified && !string.IsNullOrWhiteSpace(profile.PhoneNumber))
        {
            try
            {
                var otp = await otpService.GenerateAsync(
                    profile.PhoneNumber, OtpPurpose.PhoneVerification, cancellationToken);

                await notificationPublisher.PublishAsync(
                    NotificationMessage.WhatsApp(
                        profile.PhoneNumber,
                        $"Your Cargo verification code is {otp}. Do not share this code with anyone."),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send phone verification OTP for {PhoneNumber}", profile.PhoneNumber);
            }
        }

        var hasRejectedDocuments = profile.Documents
            .Any(d => d.ReviewStatus == DocumentReviewStatus.Rejected);

        var documents = profile.Documents
            .Select(d => new DocumentSummary(
                d.Id,
                d.DocumentType.ToString(),
                d.ReviewStatus.ToString(),
                d.ReviewNote,
                d.UploadedAt))
            .ToList();

        return new ProfileResponse(
            profile.Id,
            profile.KeycloakUserId,
            profile.Email,
            profile.FullName,
            profile.PhoneNumber,
            profile.IsEmailVerified,
            profile.OnboardingStatus.ToString(),
            hasRejectedDocuments,
            documents);
    }
}