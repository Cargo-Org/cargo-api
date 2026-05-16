using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Auth.RefreshToken;

public sealed record RefreshTokenCommand(
    string RefreshToken
) : ICommand<RefreshTokenResponse>;


public record RefreshTokenResponse(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    int RefreshExpiresIn);
