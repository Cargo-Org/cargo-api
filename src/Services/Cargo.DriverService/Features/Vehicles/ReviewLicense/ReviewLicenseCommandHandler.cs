using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Data;
using Cargo.DriverService.Domain.Enums;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Features.Vehicles.ReviewLicense;

public sealed class ReviewLicenseCommandHandler(DriverDbContext dbContext)
    : ICommandHandler<ReviewLicenseCommand>
{
    public async Task<ErrorOr<Unit>> Handle(
        ReviewLicenseCommand request,
        CancellationToken cancellationToken)
    {
        var vehicle = await dbContext.Vehicles
            .FirstOrDefaultAsync(v => v.Id == request.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            return Error.NotFound(
                code: "Vehicle.NotFound",
                description: "Vehicle not found.");
        }

        if (vehicle.LicenseReviewStatus is null)
        {
            return Error.Validation(
                code: "License.NotUploaded",
                description: "No license has been uploaded for this vehicle.");
        }

        switch (request.Status)
        {
            case VehicleLicenseStatus.Approved:
                vehicle.ApproveLicense(request.ReviewerKeycloakId);
                break;

            case VehicleLicenseStatus.Rejected:
                if (string.IsNullOrWhiteSpace(request.ReviewNote))
                {
                    return Error.Validation(
                        code: "License.ReviewNoteRequired",
                        description: "A review note is required when rejecting a license.");
                }
                vehicle.RejectLicense(request.ReviewerKeycloakId, request.ReviewNote);
                break;

            default:
                return Error.Validation(
                    code: "License.InvalidStatus",
                    description: "Status must be 'Approved' or 'Rejected'.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
