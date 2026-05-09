using Cargo.CustomerService.Domain.Enums;

namespace Cargo.CustomerService.Features.Documents.GetMyDocuments;

public record DocumentResponse(
    Guid Id,
    DocumentType DocumentType,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DocumentReviewStatus ReviewStatus,
    string? ReviewNote,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ReviewedAt,
    string? DownloadUrl // Will be null unless ReviewStatus == Approved
);