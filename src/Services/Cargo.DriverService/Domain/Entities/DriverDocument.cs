using Cargo.DriverService.Domain.Enums;

namespace Cargo.DriverService.Domain.Entities;

public sealed class DriverDocument
{
    private DriverDocument() { }

    public Guid Id { get; private set; }
    public Guid DriverId { get; private set; }

    public DriverDocumentType DocumentType { get; private set; }

    // Display only — what the user's file was named on their device.
    public string OriginalFileName { get; private set; } = null!;

    // MinIO/R2 object key. Format: drivers/{driverId}/{documentType}-{uuid}.ext
    // Always server-generated. Never client-provided.
    public string ObjectKey { get; private set; } = null!;

    // MIME type. Validated against whitelist at upload URL generation.
    public string ContentType { get; private set; } = null!;

    // Maximum 15,728,640 bytes (15 MB). Validated at POST /me/documents.
    public long FileSizeBytes { get; private set; }

    public DocumentReviewStatus ReviewStatus { get; private set; }

    // Admin rejection reason. Null when pending or approved.
    public string? ReviewNote { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    // Admin sub claim. Audit trail. Null until reviewed.
    public string? ReviewedByKeycloakId { get; private set; }

    public DriverProfile Driver { get; private set; } = null!;

    public static DriverDocument Create(
        Guid driverId,
        DriverDocumentType documentType,
        string originalFileName,
        string objectKey,
        string contentType,
        long fileSizeBytes)
    {
        return new DriverDocument
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            DocumentType = documentType,
            OriginalFileName = originalFileName,
            ObjectKey = objectKey,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            ReviewStatus = DocumentReviewStatus.Pending,
            UploadedAt = DateTimeOffset.UtcNow
        };
    }

    public void Approve(string reviewerKeycloakId)
    {
        ReviewStatus = DocumentReviewStatus.Approved;
        ReviewNote = null;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewedByKeycloakId = reviewerKeycloakId;
    }

    public void Reject(string reviewerKeycloakId, string reviewNote)
    {
        ReviewStatus = DocumentReviewStatus.Rejected;
        ReviewNote = reviewNote;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewedByKeycloakId = reviewerKeycloakId;
    }
}
