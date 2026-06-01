using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Vehicles.GetMyVehicles;

public sealed class GetMyVehiclesQueryHandler(DriverDbContext dbContext)
    : IQueryHandler<GetMyVehiclesQuery, IReadOnlyList<VehicleResponse>>
{
    public async Task<ErrorOr<IReadOnlyList<VehicleResponse>>> Handle(
        GetMyVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.KeycloakUserId == request.KeycloakUserId,
                cancellationToken);

        if (profile is null)
        {
            return Error.NotFound(
                code: "Driver.NotFound",
                description: "Driver profile not found.");
        }

        var vehicles = await dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.DriverId == profile.Id)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new VehicleResponse(
                v.Id,
                v.VehicleNumber,
                v.VehicleModel,
                v.VehicleType,
                v.VehicleColor,
                v.ManufactureYear,
                v.LicensePlate,
                v.IsLicenseVerified,
                v.LicenseReviewStatus != null
                    ? v.LicenseReviewStatus.Value.ToString()
                    : null,
                v.LicenseReviewNote,
                v.CreatedAt))
            .ToListAsync(cancellationToken);

        return vehicles;
    }
}
