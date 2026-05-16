using Cargo.BuildingBlocks.CQRS;
using MediatR;

namespace Cargo.DriverService.Features.Auth.ForgotPassword;

public sealed record ForgotPasswordCommand(
    string Email
) : ICommand<Unit>;
