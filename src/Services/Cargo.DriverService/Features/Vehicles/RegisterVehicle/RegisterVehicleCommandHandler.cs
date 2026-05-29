using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Data;
using Cargo.DriverService.Domain.Entities;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Vehicles.RegisterVehicle;

public sealed class RegisterVehicleCommandHandler(DriverDbContext dbContext)
    : ICommandHandler<RegisterVehicleCommand, Guid>
{
    public async Task<ErrorOr<Guid>> Handle(
        RegisterVehicleCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Look up the driver profile
        var profile = await dbContext.DriverProfiles
            .FirstOrDefaultAsync(
                p => p.KeycloakUserId == request.KeycloakUserId,
                cancellationToken);

        if (profile is null)
        {
            return Error.NotFound(
                code: "Driver.NotFound",
                description: "Driver profile not found.");
        }

        // 2. Check for duplicate vehicle number
        bool vehicleExists = await dbContext.Vehicles
            .AnyAsync(v => v.VehicleNumber == request.VehicleNumber, cancellationToken);

        if (vehicleExists)
        {
            return Error.Conflict(
                code: "Vehicle.AlreadyExists",
                description: $"A vehicle with number '{request.VehicleNumber}' already exists.");
        }

        // 3. Create the vehicle
        var vehicle = Vehicle.Create(
            profile.Id,
            request.VehicleNumber,
            request.VehicleModel,
            request.VehicleType,
            request.VehicleColor,
            request.ManufactureYear,
            request.LicensePlate);

        dbContext.Vehicles.Add(vehicle);

        // 4. Auto-set CurrentVehicleNumber if this is the driver's first vehicle
        profile.SetCurrentVehicleIfEmpty(vehicle.VehicleNumber);

        // 5. Persist
        await dbContext.SaveChangesAsync(cancellationToken);

        return vehicle.Id;
    }
}
