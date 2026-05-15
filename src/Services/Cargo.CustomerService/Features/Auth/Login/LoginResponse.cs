namespace Cargo.CustomerService.Features.Auth.Login;

public sealed record LoginResponse(Guid CustomerId, string? FullName, string AccessToken, string RefreshToken, int AccessTokenExpiresIn, int RefreshTokenExpiresIn);