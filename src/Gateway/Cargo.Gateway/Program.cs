using Cargo.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Observability (Traces, Metrics, Logs via OTLP)
builder.AddCargoObservability("cargo-gateway");

// Configuration values — Customer Realm
var customerAuthority = builder.Configuration["KeycloakCustomer:Authority"]
    ?? throw new InvalidOperationException("KeycloakCustomer:Authority must be configured");
var customerMetadata = builder.Configuration["KeycloakCustomer:MetadataAddress"]
    ?? throw new InvalidOperationException("KeycloakCustomer:MetadataAddress must be configured");
var customerAudience = builder.Configuration["KeycloakCustomer:Audience"]
    ?? throw new InvalidOperationException("KeycloakCustomer:Audience must be configured");

// Configuration values — Driver Realm
var driverAuthority = builder.Configuration["KeycloakDriver:Authority"]
    ?? throw new InvalidOperationException("KeycloakDriver:Authority must be configured");
var driverMetadata = builder.Configuration["KeycloakDriver:MetadataAddress"]
    ?? throw new InvalidOperationException("KeycloakDriver:MetadataAddress must be configured");
var driverAudience = builder.Configuration["KeycloakDriver:Audience"]
    ?? throw new InvalidOperationException("KeycloakDriver:Audience must be configured");

// JWT Bearer authentication — two schemes, one per realm
builder.Services
    .AddAuthentication("MultiRealm")
    .AddJwtBearer("CustomerBearer", options =>
    {
        options.Authority = customerAuthority;
        options.MetadataAddress = customerMetadata;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = customerAuthority,
            ValidateAudience = true,
            ValidAudience = customerAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    })
    .AddJwtBearer("DriverBearer", options =>
    {
        options.Authority = driverAuthority;
        options.MetadataAddress = driverMetadata;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = driverAuthority,
            ValidateAudience = true,
            ValidAudience = driverAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    })
    .AddPolicyScheme("MultiRealm", "Multi-Realm Keycloak", options =>
    {
        // Route to the correct JWT scheme based on request path
        options.ForwardDefaultSelector = context =>
        {
            var path = context.Request.Path.Value ?? "";
            if (path.StartsWith("/api/driver", StringComparison.OrdinalIgnoreCase))
                return "DriverBearer";
            return "CustomerBearer";
        };
    });

builder.Services.AddAuthorization(options =>
{
    // "default" policy — referenced by API routes in appsettings.json.
    // Requires a valid, authenticated JWT issued by either Keycloak realm.
    // Doc routes use AuthorizationPolicy: "anonymous" and bypass this entirely.
    options.DefaultPolicy = new AuthorizationPolicyBuilder("CustomerBearer", "DriverBearer")
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