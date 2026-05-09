using ErrorOr;
using MediatR;

namespace Cargo.BuildingBlocks.CQRS;

// Command with no result data — returns ErrorOr<Unit> so errors can still propagate.
// Use this for fire-and-forget operations that either succeed or fail.
public interface ICommand : ICommand<Unit>;

// Command with result data — returns ErrorOr<TResponse>.
// TResponse is the success payload. On failure, the handler returns Error values instead.
public interface ICommand<TResponse> : IRequest<ErrorOr<TResponse>>;