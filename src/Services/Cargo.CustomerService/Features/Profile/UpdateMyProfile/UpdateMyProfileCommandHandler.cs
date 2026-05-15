using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Data;
using Cargo.CustomerService.Domain.Enums;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Profile.UpdateMyProfile;

public sealed class UpdateMyProfileCommandHandler(
    CustomerDbContext dbContext)
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