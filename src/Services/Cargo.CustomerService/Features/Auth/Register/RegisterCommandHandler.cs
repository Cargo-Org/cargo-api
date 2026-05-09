using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Common.Exceptions;
using Cargo.CustomerService.Data;
using Cargo.CustomerService.Domain.Entities;
using Cargo.CustomerService.Infrastructure.Keycloak;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Auth.Register;

public sealed class RegisterCommandHandler(
    CustomerDbContext dbContext,
    IKeycloakAdminClient keycloakAdminClient,
    ILogger<RegisterCommandHandler> logger)
    : ICommandHandler<RegisterCommand, RegisterResponse>
{
    public async Task<ErrorOr<RegisterResponse>> Handle(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Duplicate check in our database ───────────────────────
        // Check our DB first — faster than a Keycloak round-trip.
        var emailExists = await dbContext.CustomerProfiles
            .AnyAsync(p => p.Email == command.Email, cancellationToken);

        if (emailExists)
            return Error.Conflict(
                code: "Registration.EmailAlreadyExists",
                description: "An account with this email address already exists.");

        // ── Step 2: Create user in Keycloak ───────────────────────────────
        string keycloakUserId;
        try
        {
            keycloakUserId = await keycloakAdminClient.CreateUserAsync(
                command.Email,
                command.Password,
                command.FullName,
                cancellationToken);
        }
        catch (ConflictException)
        {
            // User exists in Keycloak but not in our DB.
            // Could be a previous failed registration. Treat as conflict.
            return Error.Conflict(
                code: "Registration.EmailAlreadyExists",
                description: "An account with this email address already exists.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to create Keycloak user for {Email}", command.Email);
            return Error.Failure(
                code: "Registration.KeycloakError",
                description: "Failed to create user account. Please try again.");
        }

        // ── Step 3: Assign customer role ──────────────────────────────────
        try
        {
            await keycloakAdminClient.AssignRealmRoleAsync(
                keycloakUserId, "customer", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to assign role to Keycloak user {UserId}. Starting compensating delete.",
                keycloakUserId);

            await TryDeleteKeycloakUserAsync(keycloakUserId, cancellationToken);

            return Error.Failure(
                code: "Registration.RoleAssignmentFailed",
                description: "Failed to configure user account. Please try again.");
        }

        // ── Step 4: Create CustomerProfile ────────────────────────────────
        var profile = CustomerProfile.CreateForEmailRegistration(
            keycloakUserId,
            command.Email,
            command.FullName,
            command.PhoneNumber);

        dbContext.CustomerProfiles.Add(profile);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to save CustomerProfile for Keycloak user {UserId}. Starting compensating delete.",
                keycloakUserId);

            await TryDeleteKeycloakUserAsync(keycloakUserId, cancellationToken);

            return Error.Failure(
                code: "Registration.DatabaseError",
                description: "Failed to complete registration. Please try again.");
        }

        // ── Step 5: Send verification email ───────────────────────────────
        // Non-critical: profile is already saved. If this fails, the user is
        // registered and can request a new verification email later.
        try
        {
            await keycloakAdminClient.SendVerificationEmailAsync(
                keycloakUserId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send verification email to {Email}. " +
                "Registration succeeded — user must request a new verification email.",
                command.Email);
        }

        logger.LogInformation(
            "Successfully registered customer {CustomerId} for {Email}",
            profile.Id, command.Email);

        return new RegisterResponse(profile.Id);
    }

    // ── Compensating transaction ───────────────────────────────────────────
    private async Task TryDeleteKeycloakUserAsync(
        string keycloakUserId,
        CancellationToken ct)
    {
        try
        {
            await keycloakAdminClient.DeleteUserAsync(keycloakUserId, ct);
            logger.LogInformation(
                "Compensating transaction succeeded: deleted Keycloak user {UserId}",
                keycloakUserId);
        }
        catch (Exception ex)
        {
            // This is now a manual consistency issue.
            // The user exists in Keycloak with no profile in customer_db.
            // Phase 3 Outbox Pattern eliminates this risk entirely.
            logger.LogCritical(ex,
                "CONSISTENCY ALERT: Keycloak user {UserId} exists but compensating " +
                "delete failed. Manual cleanup required. Check Keycloak admin console.",
                keycloakUserId);
        }
    }
}