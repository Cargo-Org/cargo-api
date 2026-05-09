using Cargo.CustomerService.Domain.Enums;
using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Documents.GetUploadUrl;

// IQuery<TResponse> inherently wraps the response in MediatR's IRequest<ErrorOr<TResponse>>
public record GetUploadUrlQuery(
    DocumentType DocumentType,
    string ContentType,
    string KeycloakUserId) : IQuery<GetUploadUrlResponse>;