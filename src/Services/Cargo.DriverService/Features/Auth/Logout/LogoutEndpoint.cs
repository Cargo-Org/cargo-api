using Cargo.BuildingBlocks.Extensions;
using MediatR;
using System.Security.Claims;

namespace Cargo.DriverService.Features.Auth.Logout;

// Request body — only the refresh token comes from the client.
// KeycloakUserId is extracted from the JWT.
public sealed record LogoutRequest(string RefreshToken);

public static class LogoutEndpoint
{
    public static IEndpointRouteBuilder MapLogoutEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/logout", async (
            LogoutRequest request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var command = new LogoutCommand(keycloakUserId, request.RefreshToken);
            var result = await sender.Send(command, ct);

            return result.Match(
                _ => Results.Ok(new { message = "Logged out successfully." }),
                errors => errors.ToProblemResult()
            );
        })
        .RequireAuthorization()
        .WithName("Logout")
        .WithSummary("Logout and invalidate all sessions")
        .WithDescription(
            "Revokes the provided refresh token and destroys all active " +
            "Keycloak sessions for the authenticated user.");

        return app;
    }
}
