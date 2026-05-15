using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Security.Keycloak;
using ErrorOr;
using MediatR;

namespace Cargo.CustomerService.Features.Auth.Logout;

public sealed class LogoutCommandHandler(
    IKeycloakAdminClient keycloakAdminClient,
    ILogger<LogoutCommandHandler> logger)
    : ICommandHandler<LogoutCommand, Unit>
{
    public async Task<ErrorOr<Unit>> Handle(
        LogoutCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user ID from the refresh token's sub claim
            // to invalidate all Keycloak sessions.
            // The KeycloakUserId is passed via the endpoint from the JWT.
            await keycloakAdminClient.LogoutAsync(
                command.RefreshToken,
                command.KeycloakUserId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Logout failed for user {UserId}",
                command.KeycloakUserId);
            return Error.Failure(
                code: "Logout.Failed",
                description: "Logout failed. Please try again.");
        }

        logger.LogInformation(
            "User {UserId} logged out successfully.", command.KeycloakUserId);

        return Unit.Value;
    }
}
