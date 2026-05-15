using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Data;
using Cargo.CustomerService.Domain.Entities;
using Cargo.CustomerService.Domain.Enums;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Profile.GetMyProfile;

public sealed class GetMyProfileQueryHandler(
    CustomerDbContext dbContext,
    ILogger<GetMyProfileQueryHandler> logger)
    : IQueryHandler<GetMyProfileQuery, ProfileResponse>
{
    public async Task<ErrorOr<ProfileResponse>> Handle(
        GetMyProfileQuery query,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Load the profile ──────────────────────────────────────
        // Profile creation is handled by POST /register (email/password path)
        // or EnsureSocialProfileCommand (social login path, dispatched by the
        // endpoint before this query). This handler is read-only (CQS).
        var profile = await dbContext.CustomerProfiles
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(
                p => p.KeycloakUserId == query.KeycloakUserId,
                cancellationToken);

        if (profile is null)
        {
            return Error.NotFound(
                code: "Profile.NotFound",
                description: "Customer profile not found.");
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
                // Non-critical: profile was loaded. Log the sync failure
                // but do not fail the request — the user still gets their profile.
                logger.LogWarning(ex,
                    "Failed to sync IsEmailVerified for {KeycloakUserId}",
                    query.KeycloakUserId);
            }
        }

        // ── Step 3: Build and return response ─────────────────────────────
        return MapToResponse(profile);
    }

    private static ProfileResponse MapToResponse(CustomerProfile profile)
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