using Cargo.BuildingBlocks;
using Cargo.BuildingBlocks.Behaviours;
using Cargo.BuildingBlocks.Extensions;
using Cargo.BuildingBlocks.Security.Keycloak;
using Cargo.BuildingBlocks.Storage.S3;
using Cargo.DriverService.Data;
using Cargo.DriverService.Features.Auth.ForgotPassword;
using Cargo.DriverService.Features.Auth.GoogleLogin;
using Cargo.DriverService.Features.Auth.Login;
using Cargo.DriverService.Features.Auth.Logout;
using Cargo.DriverService.Features.Auth.RefreshToken;
using Cargo.DriverService.Features.Auth.Register;
using Cargo.DriverService.Features.Auth.ResetPassword;
using Cargo.DriverService.Features.Auth.VerifyEmail;
using Cargo.DriverService.Features.Documents;
using Cargo.DriverService.Features.Vehicles;
using Cargo.DriverService.Features.Profile.GetMyProfile;
using Cargo.DriverService.Features.Profile.UpdateMyProfile;
using Cargo.Observability;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Observability ────────────────────────────────────────────────
builder.AddCargoObservability("cargo-driver-service");

// ── OpenAPI / Documentation ──────────────────────────────────────
builder.Services.AddCargoOpenApi(title: "Cargo — Driver Service", servicePrefix: "driver");

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
builder.Services.AddDbContextPool<DriverDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DriverDb")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:DriverDb is required.");

    options.UseNpgsql(connectionString);

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

// ── Cargo Building Blocks Backing Services ────────────────────────
builder.Services.AddKeycloakAdmin(builder.Configuration);
builder.Services.AddOtpAndCache(builder.Configuration);
builder.Services.AddEmailService(builder.Configuration);
builder.Services.AddStorageService(builder.Configuration);

// ── Health Check ─────────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Automatic Migrations ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<DriverDbContext>();

        // This applies any pending migrations and creates the DB if it doesn't exist
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "An error occurred while migrating the database.");

        // Fail fast: If the DB isn't ready, the microservice shouldn't start
        throw;
    }
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
app.UseCargoOpenApi("driver");

// ── Middleware order — this is critical ──────────────────────────
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ───────────────────────────────────────────────────
// Health Check
app.MapHealthChecks("/health").AllowAnonymous();
// Auth
app.MapRegisterEndpoint();
app.MapLoginEndpoint();
app.MapRefreshTokenEndpoint();
app.MapVerifyEmailEndpoint();
app.MapGoogleLoginEndpoint();
app.MapForgotPasswordEndpoint();
app.MapResetPasswordEndpoint();
app.MapLogoutEndpoint();
// Profile
app.MapGetMyProfileEndpoint();
app.MapUpdateMyProfileEndpoint();
// Documents
app.MapDocumentEndpoints();
// Vehicles
app.MapVehicleEndpoints();

app.Run();
