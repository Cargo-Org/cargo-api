using Cargo.BuildingBlocks.Extensions;
using MediatR;

namespace Cargo.DriverService.Features.Auth.Register;

public static class RegisterEndpoint
{
    public static IEndpointRouteBuilder MapRegisterEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/register", async (
            RegisterCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            return result.Match(
                success => Results.Created("/me", success),
                errors => errors.ToProblemResult()
            );
        })
        .AllowAnonymous()
        .WithName("Register")
        .WithSummary("Register a new driver account")
        .WithDescription(
            "Creates a Keycloak user, assigns the driver role, " +
            "saves a DriverProfile, and sends a verification email. " +
            "Returns 201 with the new driver ID.");

        return app;
    }
}
