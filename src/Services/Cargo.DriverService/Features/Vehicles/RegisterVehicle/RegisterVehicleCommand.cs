using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Vehicles.RegisterVehicle;

public record RegisterVehicleCommand(
    string KeycloakUserId,
    string VehicleNumber,
    string VehicleModel,
    string VehicleType,
    string VehicleColor,
    int ManufactureYear,
    string LicensePlate) : ICommand<Guid>;
