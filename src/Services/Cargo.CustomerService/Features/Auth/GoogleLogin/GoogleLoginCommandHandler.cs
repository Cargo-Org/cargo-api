using Cargo.BuildingBlocks.CQRS;
using Cargo.BuildingBlocks.Security.Keycloak;
using ErrorOr;
using System.Text;
using System.Text.Json;

namespace Cargo.CustomerService.Features.Auth.GoogleLogin;

public sealed class GoogleLoginCommandHandler(
    IKeycloakAdminClient keycloakAdminClient,
    ILogger<GoogleLoginCommandHandler> logger)
    : ICommandHandler<GoogleLoginCommand, AuthTokenResponse>
{
    public async Task<ErrorOr<AuthTokenResponse>> Handle(
        GoogleLoginCommand command,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Exchange Google ID token for Keycloak tokens ─────────
        AuthTokenResponse tokens;
        try
        {
            tokens = await keycloakAdminClient.ExchangeGoogleTokenAsync(
                command.GoogleIdToken, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Google token exchange rejected.");
            return Error.Unauthorized(
                code: "GoogleLogin.InvalidToken",
                description: "Invalid or expired Google token.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Google token exchange failed unexpectedly.");
            return Error.Failure(
                code: "GoogleLogin.Failed",
                description: "Google login failed. Please try again.");
        }

        // ── Step 2: Extract user ID from returned Keycloak JWT ───────────
        string userId;
        try
        {
            userId = ExtractSubFromJwt(tokens.AccessToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract sub claim from Keycloak token.");
            return Error.Failure(
                code: "GoogleLogin.TokenParseError",
                description: "Login succeeded but token processing failed.");
        }

        // ── Step 3: Assign customer role (idempotent — safe for returning users) ─
        try
        {
            await keycloakAdminClient.AssignRealmRoleAsync(
                userId, "customer", cancellationToken);
        }
        catch (Exception ex)
        {
            // Non-fatal — log and continue. The user is authenticated.
            // A missing role would surface as authorization failures on protected
            // endpoints, which is far preferable to blocking the login entirely.
            logger.LogWarning(ex,
                "Failed to assign 'customer' role to user {UserId} after Google login. " +
                "User is authenticated but may lack permissions.", userId);
        }

        logger.LogInformation("Google login succeeded for user {UserId}", userId);

        return tokens;
    }

    /// <summary>
    /// Decodes the payload segment of a JWT (without signature verification —
    /// safe here because we received the token directly from Keycloak just above).
    /// </summary>
    private static string ExtractSubFromJwt(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            throw new InvalidOperationException("Invalid JWT format — expected 3 dot-separated segments.");

        // JWT uses base64url — restore standard base64 padding before decoding.
        var payload = parts[1];
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("sub").GetString()
            ?? throw new InvalidOperationException("JWT 'sub' claim is missing or null.");
    }
}
