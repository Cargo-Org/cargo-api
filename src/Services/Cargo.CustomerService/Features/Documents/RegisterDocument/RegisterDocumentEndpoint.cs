using Cargo.BuildingBlocks.Extensions;
using Cargo.CustomerService.Features.Auth.Register;
using MediatR;

namespace Cargo.CustomerService.Features.Documents.RegisterDocument
{
    public static class RegisterDocumentEndpoint
    {
        public static IEndpointRouteBuilder MapRegisterDocumentEndpoint(
            this IEndpointRouteBuilder app)
        {
            app.MapPost("/me/documents", async (
                RegisterDocumentCommand command,
                ISender sender,
                HttpContext httpContext,
                CancellationToken ct) =>
            {
                var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                // We overwrite whatever the client sent with the trusted token claim
                var secureCommand = command with { KeycloakUserId = userId };

                var result = await sender.Send(secureCommand, ct);

                return result.Match(
                    // Return HTTP 201 Created with the location header pointing to the document
                    documentId => Results.Created($"/me/documents/{documentId}", new { Id = documentId }),
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
}
