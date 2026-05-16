using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Security.Keycloak;
using ErrorOr;

namespace Cargo.DriverService.Features.Auth.RefreshToken;


public sealed class RefreshTokenCommandHandler(
    IKeycloakAdminClient keycloakAdminClient,
    ILogger<RefreshTokenCommandHandler> logger)
    : ICommandHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    public async Task<ErrorOr<RefreshTokenResponse>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        AuthTokenResponse userTokenResponse;
        try
        {
            userTokenResponse = await keycloakAdminClient.RefreshTokenAsync(
                command.RefreshToken,
                cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Error.Unauthorized(
                code: "Login.InvalidCredentials",
                description: "Invalid email or password.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to authenticate Keycloak user for {RefreshToken}", command.RefreshToken);
            return Error.Failure(
                code: "RefreshToken.KeycloakError",
                description: "Failed to authenticate user. Please try again.");
        }

        if (userTokenResponse is null)
        {
            return Error.Failure(
                code: "RefreshToken.InvalidResponse",
                description: "Received invalid response from authentication service.");
        }
        else
        {
            logger.LogInformation(
                "Successfully refreshed token for user with refresh token {RefreshToken}", command.RefreshToken);
        }

        return new RefreshTokenResponse(userTokenResponse.AccessToken, userTokenResponse.ExpiresIn, userTokenResponse.RefreshToken, userTokenResponse.ExpiresIn);
    }
}
