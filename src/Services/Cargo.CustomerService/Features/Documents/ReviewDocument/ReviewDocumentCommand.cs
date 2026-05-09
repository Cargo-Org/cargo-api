using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Domain.Enums;

namespace Cargo.CustomerService.Features.Documents.ReviewDocument;

// Uses ICommand which inherently returns ErrorOr<Unit>
public record ReviewDocumentCommand(
    Guid DocumentId,
    string ReviewerKeycloakId,
    DocumentReviewStatus Status,
    string? ReviewNote) : ICommand;