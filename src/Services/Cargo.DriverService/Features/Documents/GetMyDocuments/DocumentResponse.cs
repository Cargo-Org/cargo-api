using Cargo.DriverService.Domain.Enums;

namespace Cargo.DriverService.Features.Documents.GetMyDocuments;

public record DocumentResponse(
    Guid Id,
    DriverDocumentType DocumentType,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DocumentReviewStatus ReviewStatus,
    string? ReviewNote,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ReviewedAt,
    string? DownloadUrl // Will be null unless ReviewStatus == Approved
);
