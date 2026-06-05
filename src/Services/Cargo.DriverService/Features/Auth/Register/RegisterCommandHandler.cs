using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Exceptions;
using Cargo.BuildingBlocks.Messaging;
using Cargo.BuildingBlocks.Security.Keycloak;
using Cargo.BuildingBlocks.Utils.OTP;
using Cargo.DriverService.Data;
using Cargo.DriverService.Domain.Entities;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Auth.Register;

public sealed class RegisterCommandHandler(
    DriverDbContext dbContext,
    IKeycloakAdminClient keycloakAdminClient,
    IOtpService otpService,
    INotificationPublisher notificationPublisher,
    ILogger<RegisterCommandHandler> logger)
    : ICommandHandler<RegisterCommand, RegisterResponse>
{
    public async Task<ErrorOr<RegisterResponse>> Handle(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Duplicate check ──────────────────────────────────────
        var emailExists = await dbContext.DriverProfiles
            .AnyAsync(p => p.Email == command.Email, cancellationToken);

        if (emailExists)
            return Error.Conflict(
                code: "Registration.EmailAlreadyExists",
                description: "An account with this email address already exists.");

        var phoneExists = await dbContext.DriverProfiles
            .AnyAsync(p => p.PhoneNumber == command.PhoneNumber, cancellationToken);

        if (phoneExists)
            return Error.Conflict(
                code: "Registration.PhoneAlreadyExists",
                description: "An account with this phone number already exists.");

        // ── Step 2: Create user in Keycloak ─────────────────────────────
        string keycloakUserId;
        try
        {
            keycloakUserId = await keycloakAdminClient.CreateUserAsync(
                command.Email,
                command.Password,
                command.FirstName,
                command.LastName,
                cancellationToken);
        }
        catch (ConflictException)
        {
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

        // ── Step 3: Assign driver role ─────────────────────────────────
        try
        {
            await keycloakAdminClient.AssignRealmRoleAsync(
                keycloakUserId, "driver", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to assign role to {UserId}. Compensating.", keycloakUserId);
            await TryDeleteKeycloakUserAsync(keycloakUserId, cancellationToken);
            return Error.Failure(
                code: "Registration.RoleAssignmentFailed",
                description: "Failed to configure user account. Please try again.");
        }

        // ── Step 4: Persist local profile ───────────────────────────────
        var profile = DriverProfile.CreateForEmailRegistration(
            keycloakUserId,
            command.Email,
            command.FirstName,
            command.LastName,
            command.PhoneNumber);

        dbContext.DriverProfiles.Add(profile);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to save DriverProfile for {UserId}. Compensating.",
                keycloakUserId);
            await TryDeleteKeycloakUserAsync(keycloakUserId, cancellationToken);
            return Error.Failure(
                code: "Registration.DatabaseError",
                description: "Failed to complete registration. Please try again.");
        }

        // ── Step 5: Enqueue verification OTP via outbox ─────────────────
        // Non-critical: registration already succeeded. If OTP fails,
        // the user can request a new one at login.
        try
        {
            var otp = await otpService.GenerateAsync(
                command.PhoneNumber, OtpPurpose.PhoneVerification, cancellationToken);

            await notificationPublisher.PublishAsync(
                NotificationMessage.WhatsApp(
                    command.PhoneNumber,
                    $"Your Cargo verification code is {otp}. Do not share this code with anyone."),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send phone verification OTP for {PhoneNumber} after registration",
                command.PhoneNumber);
        }

        logger.LogInformation(
            "Successfully registered driver {DriverId} for {Email}",
            profile.Id, command.Email);

        return new RegisterResponse(profile.Id);
    }

    // ── Compensating transaction ─────────────────────────────────────────
    private async Task TryDeleteKeycloakUserAsync(
        string keycloakUserId, CancellationToken ct)
    {
        try
        {
            await keycloakAdminClient.DeleteUserAsync(keycloakUserId, ct);
            logger.LogInformation(
                "Compensating delete succeeded for Keycloak user {UserId}",
                keycloakUserId);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "CONSISTENCY ALERT: Keycloak user {UserId} exists but compensating " +
                "delete failed. Manual cleanup required.",
                keycloakUserId);
        }
    }
}
