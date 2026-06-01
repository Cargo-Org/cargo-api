using Cargo.BuildingBlocks.Extensions;
using MediatR;
using System.Security.Claims;

namespace Cargo.CustomerService.Features.Auth.DeleteAccount;

public static class DeleteAccountEndpoint
{
    public static IEndpointRouteBuilder MapDeleteAccountEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapDelete("/auth/account", async (
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var command = new DeleteAccountCommand(keycloakUserId);
            var result = await sender.Send(command, ct);

            return result.Match(
                _ => Results.Ok(new { message = "Account deleted successfully." }),
                errors => errors.ToProblemResult()
            );
        })
        .RequireAuthorization()
        .WithName("DeleteCustomerAccount")
        .WithSummary("Delete authenticated customer account")
        .WithDescription(
            "Permanently deletes the authenticated customer's account from the " +
            "cargo-customer Keycloak realm and the application database. " +
            "All related documents and addresses are also removed.");

        return app;
    }
}
