using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Documents.GetMyDocuments;

public record GetMyDocumentsQuery(string KeycloakUserId) : IQuery<List<DocumentResponse>>;
