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
        // ── Step 1: Load or auto-create the profile ───────────────────────
        var profile = await dbContext.CustomerProfiles
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(
                p => p.KeycloakUserId == query.KeycloakUserId,
                cancellationToken);

        if (profile is null)
        {
            // Auto-create path — Google login user calling GET /me for the first time.
            // Email/password users are created in POST /register, never here.
            logger.LogInformation(
                "Auto-creating CustomerProfile for social login user {KeycloakUserId}",
                query.KeycloakUserId);

            profile = CustomerProfile.CreateForSocialLogin(
                query.KeycloakUserId,
                query.Email);

            dbContext.CustomerProfiles.Add(profile);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to auto-create CustomerProfile for {KeycloakUserId}",
                    query.KeycloakUserId);

                return Error.Failure(
                    code: "Profile.AutoCreateFailed",
                    description: "Failed to initialise user profile. Please try again.");
            }
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