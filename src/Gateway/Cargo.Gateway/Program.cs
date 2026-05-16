using Cargo.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Observability (Traces, Metrics, Logs via OTLP)
builder.AddCargoObservability("cargo-gateway");

// Configuration values
var keycloakAuthority = builder.Configuration["Keycloak:Authority"]
    ?? throw new InvalidOperationException("Keycloak:Authority must be configured");
var keycloakMetadataAddress = builder.Configuration["Keycloak:MetadataAddress"]
    ?? throw new InvalidOperationException("Keycloak:MetadataAddress must be configured");
var keycloakAudience = builder.Configuration["Keycloak:Audience"]
    ?? throw new InvalidOperationException("Keycloak:Audience must be configured");

// JWT Bearer authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakAuthority;
        options.MetadataAddress = keycloakMetadataAddress;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakAuthority,
            ValidateAudience = true,
            ValidAudience = keycloakAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    // "default" policy — referenced by API routes in appsettings.json.
    // Requires a valid, authenticated JWT issued by Keycloak.
    // Doc routes use AuthorizationPolicy: "anonymous" and bypass this entirely.
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// YARP
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// ── Convenience redirects ─────────────────────────────────────────────────────
// Scalar's HTML page lives at /scalar/{documentName} on each service, so the
// gateway exposes it at /docs/{service}/v1.  These redirects let developers
// type /docs/customer and land on the right page without knowing the v1 suffix.
app.MapGet("/docs/{service}", (string service) =>
        Results.Redirect($"/docs/{service}/v1", permanent: false))
    .AllowAnonymous();
// ─────────────────────────────────────────────────────────────────────────────

// Health check — always anonymous, bypasses JWT
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();