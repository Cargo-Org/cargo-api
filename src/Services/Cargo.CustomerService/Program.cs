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
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Observability ────────────────────────────────────────────────
builder.AddCargoObservability("cargo-customer-service");

// ── OpenAPI / Documentation ──────────────────────────────────────
builder.Services.AddOpenApi(options =>
{
    // Set server URL to gateway URL + service prefix
    // so Scalar "Try It" sends requests to the gateway, not the service directly
    options.AddDocumentTransformer((document, context, ct) =>
    {
        var gatewayBaseUrl = builder.Configuration["Gateway:BaseUrl"]
            ?? throw new InvalidOperationException("Gateway:BaseUrl is required.");

        document.Info.Title = "Cargo — Customer Service";
        document.Info.Version = "v1";
        document.Servers = [new() { Url = $"{gatewayBaseUrl}/api/customers" }];

        // Add JWT Bearer security scheme so Scalar shows the auth input
        document.Components ??= new();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            { "Bearer", new OpenApiSecurityScheme 
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Paste your access_token here. Get one from POST /api/auth/token"
                } 
            }
        };

        return Task.CompletedTask;
    });
});

// ── Authentication — JWT Bearer ──────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority is required.");

        options.MetadataAddress = builder.Configuration["Keycloak:MetadataAddress"]
            ?? throw new InvalidOperationException("Keycloak:MetadataAddress is required.");

        options.RequireHttpsMetadata = false;
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
            ClockSkew = TimeSpan.FromSeconds(30),
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
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
});

// ── FluentValidation ─────────────────────────────────────────────
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// ── Database ──────────────────────────────────────────────────────
builder.Services.AddDbContextPool<CustomerDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("CustomerDb")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:CustomerDb is required.");

    options.UseNpgsql(connectionString);

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
builder.Services.AddHttpClient("keycloak-admin");

// ── Infrastructure Services ───────────────────────────────────────
builder.Services.AddSingleton<IKeycloakAdminClient, KeycloakAdminClient>();

var app = builder.Build();

// ── Automatic Migrations ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
    // This will apply any pending migrations to the database on startup
    dbContext.Database.Migrate();
}

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
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ── Middleware order — this is critical ──────────────────────────
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ───────────────────────────────────────────────────
app.MapHealthChecks("/health").AllowAnonymous();

app.MapRegisterEndpoint();
app.MapGetMyProfileEndpoint();
app.MapUpdateMyProfileEndpoint();
app.MapAddressEndpoints();
app.MapDocumentEndpoints();

app.Run();