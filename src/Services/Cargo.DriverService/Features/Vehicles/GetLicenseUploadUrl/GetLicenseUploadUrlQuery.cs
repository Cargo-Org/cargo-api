using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Vehicles.GetLicenseUploadUrl;

public record GetLicenseUploadUrlQuery(
    Guid VehicleId,
    string ContentType,
    string KeycloakUserId) : IQuery<GetLicenseUploadUrlResponse>;

public sealed record GetLicenseUploadUrlResponse(
    string UploadUrl,
    string ObjectKey);
