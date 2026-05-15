using Cargo.BuildingBlocks.Extensions;
using MediatR;
using System.Security.Claims;

namespace Cargo.CustomerService.Features.Profile.UpdateMyProfile;

// The request body contains only the fields the client can submit.
// KeycloakUserId is extracted from the JWT — never from the request body.
public sealed record UpdateMyProfileRequest(
    string FirstName,
    string LastName,
    string PhoneNumber);

public static class UpdateMyProfileEndpoint
{
    public static IEndpointRouteBuilder MapUpdateMyProfileEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPatch("/me", async (
            UpdateMyProfileRequest request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException(
                    "sub claim is missing from token.");

            var command = new UpdateMyProfileCommand(
                keycloakUserId,
                request.FirstName,
                request.LastName,
                request.PhoneNumber);

            var result = await sender.Send(command, ct);

            return result.Match(
                success => Results.Ok(success),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("UpdateMyProfile")
        .WithSummary("Update the authenticated customer's profile")
        .WithDescription(
            "Submits full name and phone number. Both fields are required simultaneously. " +
            "Advances OnboardingStatus from MissingProfileData to MissingFiles " +
            "if profile data was previously incomplete.");

        return app;
    }
}