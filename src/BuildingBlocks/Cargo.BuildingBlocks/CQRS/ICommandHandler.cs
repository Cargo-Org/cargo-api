using ErrorOr;
using MediatR;

namespace Cargo.BuildingBlocks.CQRS;

// Handler for commands with no result data.
public interface ICommandHandler<in TCommand>
    : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand<Unit>;

// Handler for commands with result data.
// Note: IRequestHandler<TCommand, ErrorOr<TResponse>> is what MediatR calls.
// This interface is a strongly-typed alias that makes the ErrorOr contract explicit.
public interface ICommandHandler<in TCommand, TResponse>
    : IRequestHandler<TCommand, ErrorOr<TResponse>>
    where TCommand : ICommand<TResponse>;