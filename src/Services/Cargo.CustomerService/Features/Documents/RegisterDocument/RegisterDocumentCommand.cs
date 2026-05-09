using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Domain.Enums;

namespace Cargo.CustomerService.Features.Documents.RegisterDocument;

// Notice we use your custom ICommand<TResponse>
public record RegisterDocumentCommand(
    string KeycloakUserId,
    DocumentType DocumentType,
    string ObjectKey,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes) : ICommand<Guid>;