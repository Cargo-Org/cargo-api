using Cargo.CustomerService.Common.Exceptions;
using System.Text.Json.Serialization;

namespace Cargo.CustomerService.Infrastructure.Keycloak;

public sealed class KeycloakAdminClient : IKeycloakAdminClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KeycloakAdminClient> _logger;

    // Fail-fast: all required config is read at construction time.
    // If any value is missing, the service fails at startup, not mid-request.
    private readonly string _baseUrl;
    private readonly string _realm;
    private readonly string _clientId;
    private readonly string _clientSecret;

    // Token cache — Singleton lifetime means this survives across requests.
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public KeycloakAdminClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<KeycloakAdminClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _baseUrl = configuration["Keycloak_Backend:BaseUrl"]
            ?? throw new InvalidOperationException("Keycloak_Backend:BaseUrl is required.");
        _realm = configuration["Keycloak_Backend:Realm"]
            ?? throw new InvalidOperationException("Keycloak_Backend:Realm is required.");
        _clientId = configuration["Keycloak_Backend:ClientId"]
            ?? throw new InvalidOperationException("Keycloak_Backend:ClientId is required.");
        _clientSecret = configuration["Keycloak_Backend:ClientSecret"]
            ?? throw new InvalidOperationException("Keycloak_Backend:ClientSecret is required.");
    }

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

    // ── Public API ──────────────────────────────────────────────────────────

    public async Task<string> CreateUserAsync(
        string email,
        string password,
        string fullName,
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
            firstName = fullName,  // Store full name in firstName — Keycloak is identity
            lastName = string.Empty, // store only. Canonical name lives in customer_db.
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

    public async Task SendVerificationEmailAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        using var client = _httpClientFactory.CreateClient("keycloak-admin");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsync(
            $"{_baseUrl}/admin/realms/{_realm}/users/{userId}/send-verify-email",
            null,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Verification email sent for Keycloak user {UserId}", userId);
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

    // ── Private DTOs — used only for JSON deserialization ───────────────────

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record RoleRepresentation(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);
}