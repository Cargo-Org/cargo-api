using Cargo.BuildingBlocks.Extensions;
using MediatR;

namespace Cargo.DriverService.Features.Auth.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static IEndpointRouteBuilder MapRefreshTokenEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/refresh-token", async (
            RefreshTokenCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            return result.Match(
                success => Results.Ok(success),
                errors => errors.ToProblemResult()
            );
        })
        .AllowAnonymous()
        .WithName("RefreshToken")
        .WithSummary("Refresh authentication token")
        .WithDescription(
            "Authenticates a user with Keycloak and returns a refreshed token. " +
            "Returns 200 with the updated token details.");

        return app;
    }
}
