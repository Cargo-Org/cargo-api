using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Vehicles.RegisterLicense;

public record RegisterLicenseCommand(
    Guid VehicleId,
    string KeycloakUserId,
    string ObjectKey,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes) : ICommand;
