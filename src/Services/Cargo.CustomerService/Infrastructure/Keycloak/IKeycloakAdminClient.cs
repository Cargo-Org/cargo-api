namespace Cargo.CustomerService.Infrastructure.Keycloak;

public interface IKeycloakAdminClient
{
    /// <summary>
    /// Creates a user in Keycloak and returns the new user's sub (subject) ID.
    /// Throws ConflictException if the email is already registered in Keycloak.
    /// </summary>
    Task<string> CreateUserAsync(
        string email,
        string password,
        string fullName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Assigns a realm role to a user by role name.
    /// </summary>
    Task AssignRealmRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Triggers Keycloak to send the email verification link to the user's address.
    /// </summary>
    Task SendVerificationEmailAsync(
        string userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a Keycloak user. Used as a compensating transaction when
    /// database writes fail after a user has already been created in Keycloak.
    /// </summary>
    Task DeleteUserAsync(
        string userId,
        CancellationToken cancellationToken);
} 