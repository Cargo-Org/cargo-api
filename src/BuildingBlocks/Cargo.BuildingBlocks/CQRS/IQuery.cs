using ErrorOr;
using MediatR;

namespace Cargo.BuildingBlocks.CQRS;

// Queries always have a result — there is no IQuery without TResponse.
// Returns ErrorOr<TResponse> to allow NotFound and Forbidden errors without exceptions.
public interface IQuery<TResponse> : IRequest<ErrorOr<TResponse>>;