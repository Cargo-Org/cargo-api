using Cargo.BuildingBlocks.Extensions;
using MediatR;

namespace Cargo.CustomerService.Features.Auth.ForgotPassword;

public static class ForgotPasswordEndpoint
{
    public static IEndpointRouteBuilder MapForgotPasswordEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/forgot-password", async (
            ForgotPasswordCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            return result.Match(
                _ => Results.Ok(new { message = "If this email is registered, a password reset code has been sent." }),
                errors => errors.ToProblemResult()
            );
        })
        .AllowAnonymous()
        .WithName("ForgotPassword")
        .WithSummary("Request a password reset code")
        .WithDescription(
            "Sends a one-time password reset code to the provided email address. " +
            "Always returns 200 regardless of whether the email exists to prevent enumeration.");

        return app;
    }
}
