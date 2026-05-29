using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Vehicles.RegisterLicense;

public sealed class RegisterLicenseCommandHandler(DriverDbContext dbContext)
    : ICommandHandler<RegisterLicenseCommand>
{
    public async Task<ErrorOr<Unit>> Handle(
        RegisterLicenseCommand request,
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

        // 2. Security check: object key must match expected prefix
        var expectedPrefix = $"vehicles/{vehicle.Id}/";
        if (!request.ObjectKey.StartsWith(expectedPrefix))
        {
            return Error.Forbidden(
                code: "License.Forbidden",
                description: "You do not have permission to register a license under this storage path.");
        }

        // 3. Record license metadata — resets any previous review
        vehicle.UploadLicense(
            request.ObjectKey,
            request.ContentType,
            request.OriginalFileName);

        // 4. Persist
        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
