using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Storage.S3;
using Cargo.DriverService.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Vehicles.GetLicenseUploadUrl;

public sealed class GetLicenseUploadUrlQueryHandler(
    DriverDbContext dbContext,
    IStorageService storageService)
    : IQueryHandler<GetLicenseUploadUrlQuery, GetLicenseUploadUrlResponse>
{
    public async Task<ErrorOr<GetLicenseUploadUrlResponse>> Handle(
        GetLicenseUploadUrlQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Look up the vehicle and verify ownership
        var vehicle = await dbContext.Vehicles
            .Include(v => v.Driver)
            .FirstOrDefaultAsync(v => v.Id == request.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            return Error.NotFound(
                code: "Vehicle.NotFound",
                description: "Vehicle not found.");
        }

        if (vehicle.Driver.KeycloakUserId != request.KeycloakUserId)
        {
            return Error.Forbidden(
                code: "Vehicle.Forbidden",
                description: "You do not own this vehicle.");
        }

        // 2. Map MIME type to file extension securely
        var ext = request.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            "application/pdf" => "pdf",
            _ => (string?)null
        };

        if (ext is null)
        {
            return Error.Validation(
                code: "License.InvalidContentType",
                description: "Unsupported content type. Allowed: image/jpeg, image/png, image/webp, application/pdf.");
        }

        // 3. Generate secure, server-side object key
        var objectKey = $"vehicles/{vehicle.Id}/license-{Guid.NewGuid()}.{ext}";

        // 4. Request the pre-signed URL
        var uploadUrl = await storageService.GenerateUploadUrlAsync(
            objectKey, request.ContentType, cancellationToken);

        return new GetLicenseUploadUrlResponse(uploadUrl, objectKey);
    }
}
