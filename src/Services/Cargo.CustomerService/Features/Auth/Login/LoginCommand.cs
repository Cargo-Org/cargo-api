using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Auth.Login;

public sealed record LoginCommand(
    string Email,
    string Password
) : ICommand<LoginResponse>;