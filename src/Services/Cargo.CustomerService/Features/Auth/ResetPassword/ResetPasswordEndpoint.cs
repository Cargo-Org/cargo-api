using Cargo.BuildingBlocks.Extensions;
using MediatR;

namespace Cargo.CustomerService.Features.Auth.ResetPassword;

public static class ResetPasswordEndpoint
{
    public static IEndpointRouteBuilder MapResetPasswordEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/reset-password", async (
            ResetPasswordCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            return result.Match(
                _ => Results.Ok(new { message = "Password has been reset successfully." }),
                errors => errors.ToProblemResult()
            );
        })
        .AllowAnonymous()
        .WithName("ResetPassword")
        .WithSummary("Reset password using a verification code")
        .WithDescription(
            "Validates the OTP code sent via forgot-password, resets the user's " +
            "password in Keycloak, and invalidates the OTP. Returns 200 on success.");

        return app;
    }
}
