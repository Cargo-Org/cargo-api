using Cargo.BuildingBlocks.Exceptions;
using Cargo.BuildingBlocks.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cargo.DriverService.Features.Auth.VerifyPhone;

public static class VerifyPhoneEndpoint
{
    public static IEndpointRouteBuilder MapVerifyPhoneEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/verify-phone", async (
            VerifyPhoneCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);

            return result.Match(
                _ => Results.Ok(new { message = "Phone number verified successfully." }),
                errors => errors.ToProblemResult());
        })
        .WithName("VerifyPhone")
        .WithSummary("Verify a phone number using an OTP code")
        .WithDescription("Submits an OTP code sent via WhatsApp to verify the driver's phone number.")
        .WithTags("Auth")
        .AllowAnonymous();

        return app;
    }
}
