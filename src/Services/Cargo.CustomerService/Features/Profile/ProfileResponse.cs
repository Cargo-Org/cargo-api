namespace Cargo.CustomerService.Features.Profile;

public sealed record ProfileResponse(
    Guid CustomerId,
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