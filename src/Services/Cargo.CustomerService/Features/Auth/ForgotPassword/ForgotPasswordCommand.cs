using Cargo.BuildingBlocks.CQRS;
using MediatR;

namespace Cargo.CustomerService.Features.Auth.ForgotPassword;

public sealed record ForgotPasswordCommand(
    string Email
) : ICommand<Unit>;
