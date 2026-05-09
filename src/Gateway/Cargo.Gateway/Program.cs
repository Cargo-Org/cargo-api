using Cargo.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

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

builder.Services.AddAuthorization();

// YARP
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Health check — anonymous
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference("/scalar/customers", options =>
    {
        options.Title = "Cargo — Customer Service";
        options.OpenApiRoutePattern = "/openapi/customers";
        options.Authentication = new ScalarAuthenticationOptions { };
        options.AddPreferredSecuritySchemes("Bearer");
        options.DefaultHttpClient = new(ScalarTarget.Http, ScalarClient.HttpClient);
    });

    // When Order Service is built in Phase 3:
    // app.MapScalarApiReference("/scalar/orders", options => { ... });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();