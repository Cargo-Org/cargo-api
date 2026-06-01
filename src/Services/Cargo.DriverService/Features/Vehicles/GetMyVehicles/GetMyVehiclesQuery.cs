using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Vehicles.GetMyVehicles;

public record GetMyVehiclesQuery(string KeycloakUserId)
    : IQuery<IReadOnlyList<VehicleResponse>>;
