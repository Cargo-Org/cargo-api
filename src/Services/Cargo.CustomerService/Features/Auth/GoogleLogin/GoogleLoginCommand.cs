using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Security.Keycloak;

namespace Cargo.CustomerService.Features.Auth.GoogleLogin;

public sealed record GoogleLoginCommand(
    string GoogleIdToken
) : ICommand<AuthTokenResponse>;
