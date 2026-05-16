using Cargo.BuildingBlocks.Extensions;
using MediatR;

namespace Cargo.DriverService.Features.Auth.VerifyEmail;

public static class VerifyEmailEndpoint
{
    public static IEndpointRouteBuilder MapVerifyEmailEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/verify-email", async (
            VerifyEmailCommand command,
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
        .WithName("VerifyEmail")
        .WithSummary("Verify driver's email")
        .WithDescription(
            "Verifies a driver's email using a one-time code sent to their inbox. ");

        return app;
    }
}
