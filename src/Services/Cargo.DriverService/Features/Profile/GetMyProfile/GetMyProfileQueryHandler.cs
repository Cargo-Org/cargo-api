using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Data;
using Cargo.DriverService.Domain.Entities;
using Cargo.DriverService.Domain.Enums;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Profile.GetMyProfile;

public sealed class GetMyProfileQueryHandler(
    DriverDbContext dbContext,
    ILogger<GetMyProfileQueryHandler> logger)
    : IQueryHandler<GetMyProfileQuery, ProfileResponse>
{
    public async Task<ErrorOr<ProfileResponse>> Handle(
        GetMyProfileQuery query,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Load the profile ──────────────────────────────────────
        var profile = await dbContext.DriverProfiles
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(
                p => p.KeycloakUserId == query.KeycloakUserId,
                cancellationToken);

        if (profile is null)
        {
            return Error.NotFound(
                code: "Profile.NotFound",
                description: "Driver profile not found.");
        }

        // ── Step 2: Sync email_verified from JWT ──────────────────────────
        if (profile.IsEmailVerified != query.EmailVerifiedInToken)
        {
            profile.SyncEmailVerified(query.EmailVerifiedInToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Synced IsEmailVerified={Value} for {KeycloakUserId}",
                    query.EmailVerifiedInToken, query.KeycloakUserId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to sync IsEmailVerified for {KeycloakUserId}",
                    query.KeycloakUserId);
            }
        }

        // ── Step 3: Build and return response ─────────────────────────────
        return MapToResponse(profile);
    }

    private static ProfileResponse MapToResponse(DriverProfile profile)
    {
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
