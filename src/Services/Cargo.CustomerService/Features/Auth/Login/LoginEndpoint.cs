using Cargo.BuildingBlocks.Extensions;
using MediatR;

namespace Cargo.CustomerService.Features.Auth.Login;

public static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapLoginEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/login", async (
            LoginCommand command,
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
        .WithName("Login")
        .WithSummary("Login to the application")
        .WithDescription(
            "Authenticates a user with Keycloak and returns a login response. " +
            "Returns 200 with the login details.");

        return app;
    }
}