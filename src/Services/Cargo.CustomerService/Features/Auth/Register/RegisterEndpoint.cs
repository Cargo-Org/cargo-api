using Cargo.BuildingBlocks.Extensions;
using MediatR;

namespace Cargo.CustomerService.Features.Auth.Register;

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
        .WithSummary("Register a new customer account")
        .WithDescription(
            "Creates a Keycloak user, assigns the customer role, " +
            "saves a CustomerProfile, and sends a verification email. " +
            "Returns 201 with the new customer ID.");

        return app;
    }
}