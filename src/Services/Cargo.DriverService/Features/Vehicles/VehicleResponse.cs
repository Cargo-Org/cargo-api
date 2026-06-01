namespace Cargo.DriverService.Features.Vehicles;

public sealed record VehicleResponse(
    Guid Id,
    string VehicleNumber,
    string VehicleModel,
    string VehicleType,
    string VehicleColor,
    int ManufactureYear,
    string LicensePlate,
    bool IsLicenseVerified,
    string? LicenseReviewStatus,
    string? LicenseReviewNote,
    DateTimeOffset CreatedAt);
