using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Data;
using Cargo.DriverService.Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Profile.EnsureSocialProfile;

/// <summary>
/// Idempotent command: creates a DriverProfile for a social-login user
/// if one does not already exist. No-op if the profile is already present.
/// </summary>
public sealed class EnsureSocialProfileCommandHandler(
    DriverDbContext dbContext,
    ILogger<EnsureSocialProfileCommandHandler> logger)
    : ICommandHandler<EnsureSocialProfileCommand, Unit>
{
    public async Task<ErrorOr<Unit>> Handle(
        EnsureSocialProfileCommand command,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.DriverProfiles
            .AnyAsync(
                p => p.KeycloakUserId == command.KeycloakUserId,
                cancellationToken);

        if (exists)
            return Unit.Value; // Already created — nothing to do.

        logger.LogInformation(
            "Auto-creating DriverProfile for social login user {KeycloakUserId}",
            command.KeycloakUserId);

        var profile = DriverProfile.CreateForSocialLogin(
            command.KeycloakUserId,
            command.Email);

        dbContext.DriverProfiles.Add(profile);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to auto-create DriverProfile for {KeycloakUserId}",
                command.KeycloakUserId);

            return Error.Failure(
                code: "Profile.AutoCreateFailed",
                description: "Failed to initialise user profile. Please try again.");
        }

        return Unit.Value;
    }
}
