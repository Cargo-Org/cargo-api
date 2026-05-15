namespace Cargo.BuildingBlocks.Security.Keycloak;


/// <summary>
/// DTO representing the tokens returned upon a successful user login.
/// </summary>

public record AuthTokenResponse(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    int RefreshExpiresIn);