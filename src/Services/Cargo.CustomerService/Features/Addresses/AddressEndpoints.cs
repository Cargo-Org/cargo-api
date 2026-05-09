using Cargo.BuildingBlocks.Extensions;
using Cargo.CustomerService.Features.Addresses.AddAddress;
using Cargo.CustomerService.Features.Addresses.DeleteAddress;
using Cargo.CustomerService.Features.Addresses.GetAddresses;
using Cargo.CustomerService.Features.Addresses.SetDefaultAddress;
using Cargo.CustomerService.Features.Addresses.UpdateAddress;
using MediatR;
using System.Security.Claims;

namespace Cargo.CustomerService.Features.Addresses;

// Request body records — defined here since they are only used by endpoints.
// KeycloakUserId is never in the request body — always from JWT.
public sealed record AddAddressRequest(
    string Label,
    string AddressLine,
    string City,
    string Country,
    string? PostalCode,
    double? Latitude,
    double? Longitude,
    bool IsDefault = false);

public sealed record UpdateAddressRequest(
    string Label,
    string AddressLine,
    string City,
    string Country,
    string? PostalCode,
    double? Latitude,
    double? Longitude);

public static class AddressEndpoints
{
    public static IEndpointRouteBuilder MapAddressEndpoints(
        this IEndpointRouteBuilder app)
    {
        // ── GET /me/addresses ──────────────────────────────────────────────
        app.MapGet("/me/addresses", async (
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var result = await sender.Send(
                new GetAddressesQuery(keycloakUserId), ct);

            return result.Match(
                success => Results.Ok(success),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("GetAddresses")
        .WithSummary("List all saved addresses for the authenticated customer")
        .WithDescription(
            "Returns addresses ordered by IsDefault descending, " +
            "then CreatedAt ascending.");

        // ── POST /me/addresses ─────────────────────────────────────────────
        app.MapPost("/me/addresses", async (
            AddAddressRequest request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var command = new AddAddressCommand(
                keycloakUserId,
                request.Label,
                request.AddressLine,
                request.City,
                request.Country,
                request.PostalCode,
                request.Latitude,
                request.Longitude,
                request.IsDefault);

            var result = await sender.Send(command, ct);

            return result.Match(
                success => Results.Created($"/me/addresses/{success.AddressId}", success),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("AddAddress")
        .WithSummary("Add a new address")
        .WithDescription(
            "If IsDefault is true, clears IsDefault on all existing addresses " +
            "before setting the new one as default. All changes are atomic.");

        // ── PUT /me/addresses/{id} ─────────────────────────────────────────
        app.MapPut("/me/addresses/{id:guid}", async (
            Guid id,
            UpdateAddressRequest request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var command = new UpdateAddressCommand(
                keycloakUserId,
                id,
                request.Label,
                request.AddressLine,
                request.City,
                request.Country,
                request.PostalCode,
                request.Latitude,
                request.Longitude);

            var result = await sender.Send(command, ct);

            return result.Match(
                success => Results.Ok(success),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("UpdateAddress")
        .WithSummary("Replace an existing address entirely");

        // ── DELETE /me/addresses/{id} ──────────────────────────────────────
        app.MapDelete("/me/addresses/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var result = await sender.Send(
                new DeleteAddressCommand(keycloakUserId, id), ct);

            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("DeleteAddress")
        .WithSummary("Delete an address");

        // ── PATCH /me/addresses/{id}/set-default ───────────────────────────
        app.MapPatch("/me/addresses/{id:guid}/set-default", async (
            Guid id,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var result = await sender.Send(
                new SetDefaultAddressCommand(keycloakUserId, id), ct);

            return result.Match(
                success => Results.Ok(success),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("SetDefaultAddress")
        .WithSummary("Set an address as the default")
        .WithDescription(
            "Clears IsDefault on all other addresses for this customer " +
            "and sets the target address as default. Atomic — single transaction.");

        return app;
    }
}