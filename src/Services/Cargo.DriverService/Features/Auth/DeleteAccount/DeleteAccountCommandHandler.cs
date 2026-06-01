using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Security.Keycloak;
using Cargo.DriverService.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Auth.DeleteAccount;

public sealed class DeleteAccountCommandHandler(
    DriverDbContext dbContext,
    IKeycloakAdminClient keycloakAdminClient,
    ILogger<DeleteAccountCommandHandler> logger)
    : ICommandHandler<DeleteAccountCommand, Unit>
{
    public async Task<ErrorOr<Unit>> Handle(
        DeleteAccountCommand command,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Locate the profile ───────────────────────────────────
        var profile = await dbContext.DriverProfiles
            .FirstOrDefaultAsync(
                p => p.KeycloakUserId == command.KeycloakUserId,
                cancellationToken);

        if (profile is null)
        {
            logger.LogWarning(
                "Delete account requested for unknown Keycloak user {UserId}.",
                command.KeycloakUserId);

            return Error.NotFound(
                code: "DeleteAccount.NotFound",
                description: "Account not found.");
        }

        // ── Step 2: Delete from Keycloak (cargo-driver realm) ────────────
        // Keycloak is deleted first. If this fails, the user can retry.
        // The profile still exists in the DB, so no data is lost.
        try
        {
            await keycloakAdminClient.DeleteUserAsync(
                command.KeycloakUserId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to delete Keycloak user {UserId} from cargo-driver realm.",
                command.KeycloakUserId);

            return Error.Failure(
                code: "DeleteAccount.KeycloakError",
                description: "Failed to delete account. Please try again.");
        }

        // ── Step 3: Delete from the app database ─────────────────────────
        // Cascade delete (configured in DriverProfileConfiguration) removes
        // all related DriverDocuments automatically.
        dbContext.DriverProfiles.Remove(profile);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // CONSISTENCY ALERT: Keycloak user is gone but the local profile
            // still exists. Manual cleanup or a reconciliation job is required.
            logger.LogCritical(ex,
                "CONSISTENCY ALERT: Keycloak user {UserId} was deleted from cargo-driver realm " +
                "but the DriverProfile could not be removed from the database. " +
                "Manual cleanup required.",
                command.KeycloakUserId);

            return Error.Failure(
                code: "DeleteAccount.DatabaseError",
                description: "Account deletion partially failed. Please contact support.");
        }

        logger.LogInformation(
            "Driver account {UserId} deleted from cargo-driver realm and database.",
            command.KeycloakUserId);

        return Unit.Value;
    }
}
