using Cargo.BuildingBlocks.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Cargo.BuildingBlocks.Security.Keycloak;

public sealed class KeycloakAdminClient(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakSettings> options,
    ILogger<KeycloakAdminClient> logger) : IKeycloakAdminClient
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<KeycloakAdminClient> _logger = logger;

    // Fail-fast: all required config is read at construction time via Settings class.
    // If any value is missing, the service fails at startup, not mid-request.
    private readonly string _baseUrl = string.IsNullOrWhiteSpace(options.Value?.BaseUrl)
            ? throw new InvalidOperationException("KeycloakSettings:BaseUrl is required.")
            : options.Value.BaseUrl;
    private readonly string _realm = string.IsNullOrWhiteSpace(options.Value?.Realm)
            ? throw new InvalidOperationException("KeycloakSettings:Realm is required.")
            : options.Value.Realm;
    private readonly string _clientId = string.IsNullOrWhiteSpace(options.Value?.ClientId)
            ? throw new InvalidOperationException("KeycloakSettings:ClientId is required.")
            : options.Value.ClientId;
    private readonly string _clientSecret = string.IsNullOrWhiteSpace(options.Value?.ClientSecret)
            ? throw new InvalidOperationException("KeycloakSettings:ClientSecret is required.")
            : options.Value.ClientSecret;

    // Token cache — Singleton lifetime means this survives across requests.
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    // ── Token Management ────────────────────────────────────────────────────
    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        // Fast path — token still valid, no lock needed.
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        // Slow path — acquire lock before refreshing.
        await _tokenLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring the lock.
            // Another thread may have already refreshed the token while we waited.
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _cachedToken;

            using var client = _httpClientFactory.CreateClient("keycloak-admin");

            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
            });

            var response = await client.PostAsync(
                $"{_baseUrl}/realms/{_realm}/protocol/openid-connect/token",
                tokenRequest,
                ct);

            response.EnsureSuccessStatusCode();

            var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>(
                cancellationToken: ct)
                ?? throw new InvalidOperationException("Keycloak token response was empty.");

            // Cache the token. Subtract 30 seconds as a safety buffer against
            // clock skew and network latency.
            _cachedToken = tokenData.AccessToken;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenData.ExpiresIn - 30);

            _logger.LogDebug("Keycloak admin token refreshed. Expires in {ExpiresIn}s",
                tokenData.ExpiresIn);

            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task<AuthTokenResponse> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("keycloak-admin");

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["refresh_token"] = refreshToken
        });

        var response = await client.PostAsync(
            $"{_baseUrl}/realms/{_realm}/protocol/openid-connect/token",
            tokenRequest,
            cancellationToken);

        // Keycloak typically returns a 400 Bad Request (or sometimes 401) 
        // if the refresh token is expired, revoked, or malformed.
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Failed token refresh attempt. Token may be expired or invalid.");
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        response.EnsureSuccessStatusCode();

        // We can reuse the UserTokenResponse private DTO since the payload structure is identical
        var tokenData = await response.Content.ReadFromJsonAsync<UserTokenResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Keycloak user token response was empty.");

        _logger.LogInformation("Successfully refreshed token.");

        return new AuthTokenResponse(
            tokenData.AccessToken,
            tokenData.ExpiresIn,
            tokenData.RefreshToken,
            tokenData.RefreshExpiresIn
        );
    }

    public async Task<AuthTokenResponse> ExchangeGoogleTokenAsync(
        string googleIdToken,
        CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("keycloak-admin");

        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]           = "urn:ietf:params:oauth:grant-type:token-exchange",
            ["client_id"]            = _clientId,
            ["client_secret"]        = _clientSecret,
            ["subject_token"]        = googleIdToken,
            ["subject_issuer"]       = "google",
            ["subject_token_type"]   = "urn:ietf:params:oauth:token-type:id_token",
            ["requested_token_type"] = "urn:ietf:params:oauth:token-type:refresh_token"
        });

        var response = await client.PostAsync(
            $"{_baseUrl}/realms/{_realm}/protocol/openid-connect/token",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                                    or System.Net.HttpStatusCode.BadRequest)
            {
                _logger.LogWarning(
                    "Google token exchange rejected by Keycloak: {Error}", error);
                throw new UnauthorizedAccessException("Invalid or expired Google token.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogError(
                    "Token exchange forbidden — verify the token-exchange permission on the " +
                    "Google IDP in Keycloak is granted to the cargo-backend client.");
                throw new UnauthorizedAccessException("Google login is not permitted.");
            }

            response.EnsureSuccessStatusCode(); // throws for any other 4xx/5xx
        }

        var tokenData = await response.Content.ReadFromJsonAsync<UserTokenResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty token response from Keycloak.");

        _logger.LogInformation("Successfully exchanged Google token for Keycloak token.");

        return new AuthTokenResponse(
            tokenData.AccessToken,
            tokenData.ExpiresIn,
            tokenData.RefreshToken,
            tokenData.RefreshExpiresIn
        );
    }

    // ── User Management ──────────────────────────────────────────────────────────
    public async Task<AuthTokenResponse> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("keycloak-admin");

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["username"] = email,
            ["password"] = password,
            ["scope"] = "openid profile email"
        });

        var response = await client.PostAsync(
            $"{_baseUrl}/realms/{_realm}/protocol/openid-connect/token",
            tokenRequest,
            cancellationToken);

        // 1. Handle explicit failures BEFORE EnsureSuccessStatusCode
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Failed login attempt for user {Email}. Response: {Error}", email, errorContent);
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                // Check if Keycloak is telling us the account setup (email verification) is incomplete
                // You might need to check the exact string your Keycloak version returns in your logs
                if (errorContent.Contains("Account is not fully set up") || errorContent.Contains("email not verified"))
                {
                    _logger.LogWarning("Login failed for user {Email} due to unverified email.", email);
                    throw new EmailNotVerifiedException("Email address is not verified.");
                }

                // If it's a 400 but NOT an unverified email, it might be a wrong password 
                // (Keycloak sometimes returns 400 invalid_grant for bad passwords too)
                if (errorContent.Contains("invalid_grant"))
                {
                    _logger.LogWarning("Invalid credentials for user {Email}.", email);
                    throw new UnauthorizedAccessException("Invalid email or password.");
                }
            }

            // If it's some other 4xx or 5xx error, let it throw the standard exception
            response.EnsureSuccessStatusCode();
        }

        var tokenData = await response.Content.ReadFromJsonAsync<UserTokenResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Keycloak user token response was empty.");

        _logger.LogInformation("Successfully authenticated user {Email}", email);

        return new AuthTokenResponse(
            tokenData.AccessToken,
            tokenData.ExpiresIn,
            tokenData.RefreshToken,
            tokenData.RefreshExpiresIn
        );
    }

    public async Task<string> CreateUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        using var client = _httpClientFactory.CreateClient("keycloak-admin");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var userPayload = new
        {
            username = email,
            email,
            firstName,
            lastName,
            enabled = true,
            emailVerified = false,
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = password,
                    temporary = false
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"{_baseUrl}/admin/realms/{_realm}/users",
            userPayload,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            throw new ConflictException("A user with this email address already exists in Keycloak.");

        response.EnsureSuccessStatusCode();

        // Keycloak returns the new user's URL in the Location header.
        // Format: http://keycloak:8080/admin/realms/cargo/users/{userId}
        var location = response.Headers.Location
            ?? throw new InvalidOperationException(
                "Keycloak returned 201 but did not include a Location header.");

        // The user ID is the last segment of the URL path.
        var userId = location.Segments[^1].TrimEnd('/');

        _logger.LogInformation("Created Keycloak user {UserId} for {Email}", userId, email);

        return userId;
    }

    public async Task AssignRealmRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        using var client = _httpClientFactory.CreateClient("keycloak-admin");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Step 1: Fetch the role object — we need the role's ID, not just its name.
        var roleResponse = await client.GetAsync(
            $"{_baseUrl}/admin/realms/{_realm}/roles/{roleName}",
            cancellationToken);

        roleResponse.EnsureSuccessStatusCode();

        var role = await roleResponse.Content.ReadFromJsonAsync<RoleRepresentation>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Could not deserialise role '{roleName}'.");

        // Step 2: Assign the role to the user.
        var assignResponse = await client.PostAsJsonAsync(
            $"{_baseUrl}/admin/realms/{_realm}/users/{userId}/role-mappings/realm",
            new[] { role },
            cancellationToken);

        assignResponse.EnsureSuccessStatusCode();

        _logger.LogInformation("Assigned role '{Role}' to Keycloak user {UserId}", roleName, userId);
    }

    public async Task DeleteUserAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        using var client = _httpClientFactory.CreateClient("keycloak-admin");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync(
            $"{_baseUrl}/admin/realms/{_realm}/users/{userId}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Deleted Keycloak user {UserId}", userId);
    }

    public async Task<string?> GetUserIdByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        using var client = _httpClientFactory.CreateClient("keycloak-admin");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Escape the email and strictly enforce exact matching
        var requestUri = $"{_baseUrl}/admin/realms/{_realm}/users?email={Uri.EscapeDataString(email)}&exact=true";

        var response = await client.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<UserSearchResponse>>(
            cancellationToken: cancellationToken);

        // Return the ID of the first match, or null if empty
        return users?.FirstOrDefault()?.Id;
    }

    public async Task UpdateUserEmailVerifiedAsync(
        string userId,
        bool emailVerified,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        using var client = _httpClientFactory.CreateClient("keycloak-admin");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Constructing the partial update payload
        var updatePayload = new { emailVerified };

        var response = await client.PutAsJsonAsync(
            $"{_baseUrl}/admin/realms/{_realm}/users/{userId}",
            updatePayload,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation(
            "Updated emailVerified status to {Status} for Keycloak user {UserId}",
            emailVerified,
            userId);
    }

    public async Task ResetPasswordAsync(
        string userId,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        using var client = _httpClientFactory.CreateClient("keycloak-admin");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            type = "password",
            value = newPassword,
            temporary = false
        };

        var response = await client.PutAsJsonAsync(
            $"{_baseUrl}/admin/realms/{_realm}/users/{userId}/reset-password",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Password reset for Keycloak user {UserId}", userId);
    }

    public async Task LogoutAsync(
        string refreshToken,
        string userId,
        CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("keycloak-admin");

        // Step 1: Revoke the refresh token via the standard OIDC revocation endpoint.
        var revokeRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["token"] = refreshToken,
            ["token_type_hint"] = "refresh_token"
        });

        var revokeResponse = await client.PostAsync(
            $"{_baseUrl}/realms/{_realm}/protocol/openid-connect/revoke",
            revokeRequest,
            cancellationToken);

        revokeResponse.EnsureSuccessStatusCode();

        // Step 2: Destroy all Keycloak sessions for this user via the Admin API.
        var adminToken = await GetAccessTokenAsync(cancellationToken);
        using var adminClient = _httpClientFactory.CreateClient("keycloak-admin");
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var sessionsResponse = await adminClient.DeleteAsync(
            $"{_baseUrl}/admin/realms/{_realm}/users/{userId}/sessions",
            cancellationToken);

        // 204 = success, 404 = no active sessions (both are fine)
        if (sessionsResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
            sessionsResponse.EnsureSuccessStatusCode();

        _logger.LogInformation(
            "Logged out Keycloak user {UserId}: token revoked, sessions destroyed.",
            userId);
    }

    // ── Private DTOs — used only for JSON deserialization ───────────────────
    private sealed record UserTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("refresh_expires_in")] int RefreshExpiresIn);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record RoleRepresentation(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);

    private sealed record UserSearchResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("email")] string Email);
}