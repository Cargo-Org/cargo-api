using Cargo.BuildingBlocks.Behaviours;
using Cargo.CustomerService.Data;
using Cargo.CustomerService.Features.Addresses;
using Cargo.CustomerService.Features.Auth.Register;
using Cargo.CustomerService.Features.Documents;
using Cargo.CustomerService.Features.Profile.GetMyProfile;
using Cargo.CustomerService.Features.Profile.UpdateMyProfile;
using Cargo.CustomerService.Infrastructure.Keycloak;
using Cargo.CustomerService.Infrastructure.Storage;
using Cargo.Observability;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Observability ────────────────────────────────────────────────
builder.AddCargoObservability("cargo-customer-service");

// ── OpenAPI / Documentation ──────────────────────────────────────
builder.Services.AddOpenApi();

// ── Authentication — JWT Bearer ──────────────────────────────────
// Concept: Every service validates JWTs independently (defense in depth).
// The gateway already validated this token. We validate it again anyway.
// RequireHttpsMetadata is false for local dev only. Set true in production.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Authority is the issuer URL — what the token's iss claim must match.
        // Used for discovery but ValidIssuer is what actually validates.
        options.Authority = builder.Configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority is required.");

        // MetadataAddress is the backchannel URL — used ONLY inside Docker.
        // Fetches the JWKS (JSON Web Key Set) signing keys from Keycloak
        // using the Docker internal hostname, not localhost.
        options.MetadataAddress = builder.Configuration["Keycloak:MetadataAddress"]
            ?? throw new InvalidOperationException("Keycloak:MetadataAddress is required.");

        options.RequireHttpsMetadata = false; // false for local dev only

        // Preserve JWT claim names exactly as issued by Keycloak.
        // When true (the default), .NET remaps 'sub' to the long-form
        // NameIdentifier URN, breaking FindFirstValue("sub") calls.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Keycloak:Authority"]
                ?? throw new InvalidOperationException("Keycloak:Authority is required."),

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Keycloak:Audience"]
                ?? throw new InvalidOperationException("Keycloak:Audience is required."),

            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            // ClockSkew: allows 30 seconds of clock drift between machines.
            // Zero skew is too strict for distributed systems.
            // The default is 5 minutes which is too generous.
            ClockSkew = TimeSpan.FromSeconds(30),

            // NameClaimType: tells .NET which JWT claim to use as User.Identity.Name
            // We use sub — the permanent identity anchor.
            NameClaimType = "sub"
        };
    });

// ── Authorization ────────────────────────────────────────────────
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminPolicy", policy =>
    policy
    .RequireAuthenticatedUser()
    .RequireAssertion(context =>
    {
        var realmAccess = context.User.FindFirstValue("realm_access");
        if (realmAccess is null) return false;

        using var doc = JsonDocument.Parse(realmAccess);
        if (!doc.RootElement.TryGetProperty("roles", out var roles))
            return false;

        return roles.EnumerateArray()
            .Any(r => r.GetString() == "admin");
    }));

// ── MediatR ──────────────────────────────────────────────────────
// Scans this assembly for all IRequestHandler implementations.
// Pipeline behaviors are executed in registration order:
// LoggingBehavior wraps ValidationBehavior wraps the handler.
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
});

// ── FluentValidation ─────────────────────────────────────────────
// Scans this assembly for all AbstractValidator implementations.
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// ── Database ──────────────────────────────────────────────────────
builder.Services.AddDbContextPool<CustomerDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("CustomerDb")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:CustomerDb is required.");

    options.UseNpgsql(connectionString);

    // In development, log EF Core queries and enable detailed errors.
    // Never enable sensitive data logging in production — it logs parameter values.
    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

// ── Storage Service ───────────────────────────────────────────────
builder.Services.AddScoped<IStorageService, StorageService>();

// ── Health Check ─────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── HTTP Client Factory ───────────────────────────────────────────
// Named client for Keycloak Admin API calls.
// BaseAddress is NOT set here — KeycloakAdminClient builds full URLs
// from configuration to keep the factory registration generic.
builder.Services.AddHttpClient("keycloak-admin");

// ── Infrastructure Services ───────────────────────────────────────
// Singleton: caches the Keycloak admin token across requests.
// IHttpClientFactory is injected — never HttpClient directly into a Singleton.
builder.Services.AddSingleton<IKeycloakAdminClient, KeycloakAdminClient>();

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new
    {
        status = 500,
        title = "An unexpected error occurred.",
        detail = "The server encountered an internal error. Please try again later."
    });
}));
// ── Documentation (Development only) ────────────────────────────
// MapOpenApi generates the /openapi/v1.json document.
// MapScalarApiReference renders the interactive UI at /scalar/v1.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// ── Middleware order — this is critical ──────────────────────────
// Each middleware calls the next one in the chain.
// Authentication must come before Authorization.
// Both must come before endpoint mapping.
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ───────────────────────────────────────────────────
app.MapHealthChecks("/health").AllowAnonymous();

// Feature endpoints will be mapped here in Steps 2.5 through 2.8.
// Example pattern (do not write yet — placeholder comment only):
// app.MapPost("/register", ...).AllowAnonymous();
// app.MapGet("/me", ...).RequireAuthorization();
app.MapRegisterEndpoint();
app.MapGetMyProfileEndpoint();
app.MapUpdateMyProfileEndpoint();
app.MapAddressEndpoints();
app.MapDocumentEndpoints();

app.Run();