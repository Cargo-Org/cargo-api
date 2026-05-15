namespace Cargo.BuildingBlocks.Security.Keycloak;

public interface IKeycloakAdminClient
{
    /// <summary>
    /// Retrieves a user's unique Keycloak ID (sub) by their exact email address.
    /// Returns null if the user is not found.
    /// </summary>
    Task<string?> GetUserIdByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates a user's email verification status via a partial update.
    /// </summary>
    Task UpdateUserEmailVerifiedAsync(
        string userId,
        bool emailVerified,
        CancellationToken cancellationToken);

    /// <summary>
    /// Authenticates a user using their email and password and returns JWT tokens.
    /// Throws UnauthorizedAccessException if credentials are invalid.
    /// </summary>
    Task<AuthTokenResponse> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a valid refresh token for a new set of access and refresh tokens.
    /// Throws UnauthorizedAccessException if the refresh token is invalid, revoked, or expired.
    /// </summary>
    Task<AuthTokenResponse> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a Google ID token (obtained by the mobile app via Google Sign-In SDK)
    /// for a Keycloak-issued access + refresh token pair using RFC 8693 Token Exchange.
    /// Requires KC_FEATURES=token-exchange and a Google IDP configured in Keycloak.
    /// </summary>
    Task<AuthTokenResponse> ExchangeGoogleTokenAsync(
        string googleIdToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a user in Keycloak and returns the new user's sub (subject) ID.
    /// Throws ConflictException if the email is already registered in Keycloak.
    /// </summary>
    Task<string> CreateUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Assigns a realm role to a user by role name.
    /// </summary>
    Task AssignRealmRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a Keycloak user. Used as a compensating transaction when
    /// database writes fail after a user has already been created in Keycloak.
    /// </summary>
    Task DeleteUserAsync(
        string userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resets a user's password via the Keycloak Admin API.
    /// Used by the forgot-password flow after OTP validation.
    /// </summary>
    Task ResetPasswordAsync(
        string userId,
        string newPassword,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revokes a refresh token and destroys all Keycloak sessions for the user.
    /// Provides full server-side logout with session invalidation.
    /// </summary>
    Task LogoutAsync(
        string refreshToken,
        string userId,
        CancellationToken cancellationToken);
}