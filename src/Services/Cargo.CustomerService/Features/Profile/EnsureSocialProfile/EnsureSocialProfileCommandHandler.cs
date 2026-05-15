using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Data;
using Cargo.CustomerService.Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Profile.EnsureSocialProfile;

/// <summary>
/// Idempotent command: creates a CustomerProfile for a social-login user
/// if one does not already exist. No-op if the profile is already present.
/// </summary>
public sealed class EnsureSocialProfileCommandHandler(
    CustomerDbContext dbContext,
    ILogger<EnsureSocialProfileCommandHandler> logger)
    : ICommandHandler<EnsureSocialProfileCommand, Unit>
{
    public async Task<ErrorOr<Unit>> Handle(
        EnsureSocialProfileCommand command,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.CustomerProfiles
            .AnyAsync(
                p => p.KeycloakUserId == command.KeycloakUserId,
                cancellationToken);

        if (exists)
            return Unit.Value; // Already created — nothing to do.

        logger.LogInformation(
            "Auto-creating CustomerProfile for social login user {KeycloakUserId}",
            command.KeycloakUserId);

        var profile = CustomerProfile.CreateForSocialLogin(
            command.KeycloakUserId,
            command.Email);

        dbContext.CustomerProfiles.Add(profile);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to auto-create CustomerProfile for {KeycloakUserId}",
                command.KeycloakUserId);

            return Error.Failure(
                code: "Profile.AutoCreateFailed",
                description: "Failed to initialise user profile. Please try again.");
        }

        return Unit.Value;
    }
}
