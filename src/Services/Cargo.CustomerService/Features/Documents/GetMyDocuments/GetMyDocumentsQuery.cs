using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Documents.GetMyDocuments;

public record GetMyDocumentsQuery(string KeycloakUserId) : IQuery<List<DocumentResponse>>;