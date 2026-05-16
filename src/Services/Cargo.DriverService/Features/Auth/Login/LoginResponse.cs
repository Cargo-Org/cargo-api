namespace Cargo.DriverService.Features.Auth.Login;

public sealed record LoginResponse(Guid DriverId, string? FullName, string AccessToken, string RefreshToken, int AccessTokenExpiresIn, int RefreshTokenExpiresIn);
