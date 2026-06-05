namespace Cargo.DriverService.Features.Profile;

public sealed record ProfileResponse(
    Guid DriverId,
    string KeycloakUserId,
    string Email,
    string? FullName,
    string? PhoneNumber,
    bool IsEmailVerified,
    bool IsPhoneVerified,
    string OnboardingStatus,
    bool HasRejectedDocuments,
    IReadOnlyList<DocumentSummary> Documents
);

public sealed record DocumentSummary(
    Guid DocumentId,
    string DocumentType,
    string ReviewStatus,
    string? ReviewNote,
    DateTimeOffset UploadedAt
);
