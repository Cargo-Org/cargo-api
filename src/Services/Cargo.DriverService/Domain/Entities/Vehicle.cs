using Cargo.DriverService.Domain.Enums;

namespace Cargo.DriverService.Domain.Entities;

public sealed class Vehicle
{
    private Vehicle() { }

    public Guid Id { get; private set; }

    // Unique vehicle identifier — natural key, but not PK.
    public string VehicleNumber { get; private set; } = null!;

    // FK to DriverProfile.Id — the owner of this vehicle.
    public Guid DriverId { get; private set; }

    public string VehicleModel { get; private set; } = null!;
    public string VehicleType { get; private set; } = null!;
    public string VehicleColor { get; private set; } = null!;
    public int ManufactureYear { get; private set; }
    public string LicensePlate { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    // Computed from LicenseReviewStatus — never set directly by client.
    public bool IsLicenseVerified { get; private set; }

    // ── License file metadata ──────────────────────────────────────────────
    // All null until the driver uploads a license for the first time.

    // S3 object key. Format: vehicles/{vehicleId}/license-{uuid}.ext
    public string? LicenseObjectKey { get; private set; }
    public string? LicenseContentType { get; private set; }
    public string? LicenseOriginalFileName { get; private set; }
    public DateTimeOffset? LicenseUploadedAt { get; private set; }

    // Null until first upload, then Pending → Approved/Rejected.
    public VehicleLicenseStatus? LicenseReviewStatus { get; private set; }
    public string? LicenseReviewNote { get; private set; }
    public DateTimeOffset? LicenseReviewedAt { get; private set; }
    public string? LicenseReviewedByKeycloakId { get; private set; }

    // Navigation property — EF Core uses this to resolve the FK.
    public DriverProfile Driver { get; private set; } = null!;

    // ── Factory method ─────────────────────────────────────────────────────
    public static Vehicle Create(
        Guid driverId,
        string vehicleNumber,
        string vehicleModel,
        string vehicleType,
        string vehicleColor,
        int manufactureYear,
        string licensePlate)
    {
        return new Vehicle
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            VehicleNumber = vehicleNumber,
            VehicleModel = vehicleModel,
            VehicleType = vehicleType,
            VehicleColor = vehicleColor,
            ManufactureYear = manufactureYear,
            LicensePlate = licensePlate,
            IsLicenseVerified = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    // ── Domain methods ─────────────────────────────────────────────────────

    /// <summary>
    /// Records license file metadata after successful S3 upload.
    /// Resets any previous review — admin must re-review.
    /// </summary>
    public void UploadLicense(
        string objectKey,
        string contentType,
        string originalFileName)
    {
        LicenseObjectKey = objectKey;
        LicenseContentType = contentType;
        LicenseOriginalFileName = originalFileName;
        LicenseUploadedAt = DateTimeOffset.UtcNow;
        LicenseReviewStatus = VehicleLicenseStatus.Pending;
        LicenseReviewNote = null;
        LicenseReviewedAt = null;
        LicenseReviewedByKeycloakId = null;
        IsLicenseVerified = false;
    }

    public void ApproveLicense(string reviewerKeycloakId)
    {
        LicenseReviewStatus = VehicleLicenseStatus.Approved;
        LicenseReviewNote = null;
        LicenseReviewedAt = DateTimeOffset.UtcNow;
        LicenseReviewedByKeycloakId = reviewerKeycloakId;
        IsLicenseVerified = true;
    }

    public void RejectLicense(string reviewerKeycloakId, string reviewNote)
    {
        LicenseReviewStatus = VehicleLicenseStatus.Rejected;
        LicenseReviewNote = reviewNote;
        LicenseReviewedAt = DateTimeOffset.UtcNow;
        LicenseReviewedByKeycloakId = reviewerKeycloakId;
        IsLicenseVerified = false;
    }
}
