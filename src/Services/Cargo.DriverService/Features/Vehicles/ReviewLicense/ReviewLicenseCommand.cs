using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Domain.Enums;

namespace Cargo.DriverService.Features.Vehicles.ReviewLicense;

public record ReviewLicenseCommand(
    Guid VehicleId,
    string ReviewerKeycloakId,
    VehicleLicenseStatus Status,
    string? ReviewNote) : ICommand;
