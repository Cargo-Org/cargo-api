using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Domain.Enums;

namespace Cargo.DriverService.Features.Documents.ReviewDocument;

public record ReviewDocumentCommand(
    Guid DocumentId,
    string ReviewerKeycloakId,
    DocumentReviewStatus Status,
    string? ReviewNote) : ICommand;
