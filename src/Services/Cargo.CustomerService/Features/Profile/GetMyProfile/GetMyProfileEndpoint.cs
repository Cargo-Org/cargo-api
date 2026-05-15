using Cargo.BuildingBlocks.Extensions;
using Cargo.CustomerService.Features.Profile.EnsureSocialProfile;
using MediatR;
using System.Security.Claims;

namespace Cargo.CustomerService.Features.Profile.GetMyProfile;

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
            // Extract identity anchor — never use email or preferred_username.
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException(
                    "sub claim is missing from token.");

            // Extract email for auto-create path.
            var email = user.FindFirstValue("email")
                ?? throw new InvalidOperationException(
                    "email claim is missing from token.");

            // Extract email_verified for sync.
            // The claim value is the string "true" or "false".
            var emailVerified = string.Equals(
                user.FindFirstValue("email_verified"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            // Ensure profile exists for social-login users (idempotent).
            // For email/password users the profile already exists — this is a no-op.
            var ensureResult = await sender.Send(
                new EnsureSocialProfileCommand(keycloakUserId, email), ct);

            if (ensureResult.IsError)
                return ensureResult.Errors.ToProblemResult();

            var query = new GetMyProfileQuery(keycloakUserId, email, emailVerified);
            var result = await sender.Send(query, ct);

            return result.Match(
                success => Results.Ok(success),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("GetMyProfile")
        .WithSummary("Get the authenticated customer's profile")
        .WithDescription(
            "Returns the full customer profile including onboarding status, " +
            "email verification state, and document list. " +
            "Auto-creates a profile for first-time Google login users. " +
            "Syncs email_verified from the JWT on every call.");

        return app;
    }
}
