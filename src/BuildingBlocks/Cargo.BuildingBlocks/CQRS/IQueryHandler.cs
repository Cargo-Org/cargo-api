using ErrorOr;
using MediatR;

namespace Cargo.BuildingBlocks.CQRS;

public interface IQueryHandler<in TQuery, TResponse>
    : IRequestHandler<TQuery, ErrorOr<TResponse>>
    where TQuery : IQuery<TResponse>;