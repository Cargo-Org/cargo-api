using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Domain.Enums;

namespace Cargo.DriverService.Features.Documents.RegisterDocument;

public record RegisterDocumentCommand(
    string KeycloakUserId,
    DriverDocumentType DocumentType,
    string ObjectKey,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes) : ICommand<Guid>;
