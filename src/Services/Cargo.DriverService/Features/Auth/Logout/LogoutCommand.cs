using Cargo.BuildingBlocks.CQRS;
using MediatR;

namespace Cargo.DriverService.Features.Auth.Logout;

public sealed record LogoutCommand(
    string KeycloakUserId,
    string RefreshToken
) : ICommand<Unit>;
