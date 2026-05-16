using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Security.Keycloak;

namespace Cargo.DriverService.Features.Auth.GoogleLogin;

public sealed record GoogleLoginCommand(
    string GoogleIdToken
) : ICommand<AuthTokenResponse>;
