using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Auth.Login;

public sealed record LoginCommand(
    string Email,
    string Password
) : ICommand<LoginResponse>;
