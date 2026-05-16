using Cargo.BuildingBlocks.Extensions;
using MediatR;

namespace Cargo.DriverService.Features.Auth.GoogleLogin;

public static class GoogleLoginEndpoint
{
    public static IEndpointRouteBuilder MapGoogleLoginEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/google", async (
            GoogleLoginCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            return result.Match(
                tokens => Results.Ok(tokens),
                errors => errors.ToProblemResult()
            );
        })
        .AllowAnonymous()
        .WithName("GoogleLogin")
        .WithSummary("Sign in with Google")
        .WithDescription(
            "Exchanges a Google ID token (obtained by the mobile app via Google Sign-In SDK) " +
            "for Cargo access and refresh tokens. Creates a new user account automatically " +
            "if this is the first Google sign-in. Profile completion is handled via GET /me.");

        return app;
    }
}
