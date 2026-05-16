using Cargo.BuildingBlocks.CQRS;
using Cargo.DriverService.Domain.Enums;

namespace Cargo.DriverService.Features.Documents.GetUploadUrl;

public record GetUploadUrlQuery(
    DriverDocumentType DocumentType,
    string ContentType,
    string KeycloakUserId) : IQuery<GetUploadUrlResponse>;
