using Cargo.BuildingBlocks.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Cargo.CustomerService.Features.Auth.RequestEmailVerification;

public static class RequestEmailVerificationEndpoint
{
    public static IEndpointRouteBuilder MapRequestEmailVerificationEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/request-email-verification", async (
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim is missing from token.");

            var command = new RequestEmailVerificationCommand(keycloakUserId);
            var result = await sender.Send(command, ct);

            return result.Match(
                _ => Results.Ok(new { message = "Email verification code sent successfully." }),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("RequestEmailVerification")
        .WithSummary("Request an email verification OTP")
        .WithDescription("Triggers an email containing an OTP to verify the authenticated customer's email address.")
        .WithTags("Auth");

        return app;
    }
}
