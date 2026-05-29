using Cargo.BuildingBlocks.Extensions;
using Cargo.DriverService.Domain.Enums;
using Cargo.DriverService.Features.Vehicles.GetLicenseUploadUrl;
using Cargo.DriverService.Features.Vehicles.GetMyVehicles;
using Cargo.DriverService.Features.Vehicles.RegisterLicense;
using Cargo.DriverService.Features.Vehicles.RegisterVehicle;
using Cargo.DriverService.Features.Vehicles.ReviewLicense;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Cargo.DriverService.Features.Vehicles;

// ── Request body records — defined here since they are only used by endpoints ──

public sealed record RegisterVehicleRequest(
    string VehicleNumber,
    string VehicleModel,
    string VehicleType,
    string VehicleColor,
    int ManufactureYear,
    string LicensePlate);

public sealed record RegisterLicenseRequest(
    string ObjectKey,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes);

public sealed record ReviewLicenseRequest(
    VehicleLicenseStatus Status,
    string? ReviewNote);

public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(
        this IEndpointRouteBuilder app)
    {
        // ── POST /me/vehicles ──────────────────────────────────────────────
        app.MapPost("/me/vehicles", async (
            RegisterVehicleRequest request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var command = new RegisterVehicleCommand(
                keycloakUserId,
                request.VehicleNumber,
                request.VehicleModel,
                request.VehicleType,
                request.VehicleColor,
                request.ManufactureYear,
                request.LicensePlate);

            var result = await sender.Send(command, ct);

            return result.Match(
                id => Results.Created($"/me/vehicles/{id}", new { Id = id }),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("RegisterVehicle")
        .WithSummary("Register a new vehicle")
        .WithDescription(
            "Registers a new vehicle under the authenticated driver. " +
            "If this is the driver's first vehicle, it will be set as the current vehicle.");

        // ── GET /me/vehicles ───────────────────────────────────────────────
        app.MapGet("/me/vehicles", async (
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var query = new GetMyVehiclesQuery(keycloakUserId);
            var result = await sender.Send(query, ct);

            return result.Match(
                success => Results.Ok(success),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("GetMyVehicles")
        .WithSummary("List all vehicles for the authenticated driver")
        .WithDescription(
            "Returns all vehicles owned by the authenticated driver, " +
            "ordered by creation date (newest first).");

        // ── GET /me/vehicles/license-upload-url ────────────────────────────
        app.MapGet("/me/vehicles/license-upload-url", async (
            [FromQuery] Guid vehicleId,
            [FromQuery] string contentType,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var query = new GetLicenseUploadUrlQuery(
                vehicleId,
                contentType,
                keycloakUserId);

            var result = await sender.Send(query, ct);

            return result.Match(
                success => Results.Ok(success),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("GetLicenseUploadUrl")
        .WithSummary("Get a pre-signed S3 URL for uploading a vehicle license")
        .WithDescription(
            "Generates a secure, temporary URL for direct-to-S3 license upload. " +
            "Supported types: image/jpeg, image/png, image/webp, application/pdf.");

        // ── POST /me/vehicles/{id}/license ─────────────────────────────────
        app.MapPost("/me/vehicles/{id:guid}/license", async (
            Guid id,
            RegisterLicenseRequest request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var command = new RegisterLicenseCommand(
                id,
                keycloakUserId,
                request.ObjectKey,
                request.OriginalFileName,
                request.ContentType,
                request.FileSizeBytes);

            var result = await sender.Send(command, ct);

            return result.Match(
                _ => Results.Ok(),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("RegisterVehicleLicense")
        .WithSummary("Register an uploaded vehicle license")
        .WithDescription(
            "Records the license file metadata after successful S3 upload. " +
            "Resets any previous license review — admin must re-review.");

        // ── PATCH /admin/vehicles/{id}/license/review ──────────────────────
        app.MapPatch("/admin/vehicles/{id:guid}/license/review", async (
            Guid id,
            ReviewLicenseRequest request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var reviewerId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var command = new ReviewLicenseCommand(
                id,
                reviewerId,
                request.Status,
                request.ReviewNote);

            var result = await sender.Send(command, ct);

            return result.Match(
                _ => Results.Ok(),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization("AdminPolicy")
        .WithName("ReviewVehicleLicense")
        .WithSummary("Approve or reject a vehicle license (Admin Only)")
        .WithDescription(
            "Updates the license review status. A review note is required " +
            "when rejecting. Sets IsLicenseVerified accordingly.");

        return app;
    }
}
