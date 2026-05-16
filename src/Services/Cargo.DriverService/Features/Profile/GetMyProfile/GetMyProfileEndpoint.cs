using Cargo.BuildingBlocks.Extensions;
using Cargo.DriverService.Features.Profile.EnsureSocialProfile;
using MediatR;
using System.Security.Claims;

namespace Cargo.DriverService.Features.Profile.GetMyProfile;

public static class GetMyProfileEndpoint
{
    public static IEndpointRouteBuilder MapGetMyProfileEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", async (
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim is missing from token.");

            var email = user.FindFirstValue("email")
                ?? throw new InvalidOperationException("email claim is missing from token.");

            var emailVerified = bool.TryParse(
                user.FindFirstValue("email_verified"), out bool v) && v;

            // Ensure a DriverProfile exists for social login users.
            await sender.Send(
                new EnsureSocialProfileCommand(keycloakUserId, email), ct);

            var query = new GetMyProfileQuery(keycloakUserId, email, emailVerified);
            var result = await sender.Send(query, ct);

            return result.Match(
                success => Results.Ok(success),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("GetMyProfile")
        .WithSummary("Get the authenticated driver's profile")
        .WithDescription(
            "Returns the current driver's profile, including onboarding status " +
            "and document summaries. Auto-creates a profile for social login " +
            "users if one does not exist.");

        return app;
    }
}
