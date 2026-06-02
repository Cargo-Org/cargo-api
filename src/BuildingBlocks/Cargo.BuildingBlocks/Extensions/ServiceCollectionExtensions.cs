using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Cargo.BuildingBlocks.Security.Keycloak;
using Cargo.BuildingBlocks.Utils.OTP;
using Cargo.BuildingBlocks.Utils.Cache;
using Cargo.BuildingBlocks.Messaging;
using Cargo.BuildingBlocks.Messaging.Outbox;
using Cargo.BuildingBlocks.Storage.S3;

namespace Cargo.BuildingBlocks;

public static class ServiceCollectionExtensions
{
    // 1. Install Keycloak
    public static IServiceCollection AddKeycloakAdmin(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<KeycloakSettings>(config.GetSection("Keycloak_Backend"));
        services.AddHttpClient("keycloak-admin"); // Registers the named HttpClient
        services.AddSingleton<IKeycloakAdminClient, KeycloakAdminClient>();
        return services;
    }

    // 2. Install OTP & Cache
    public static IServiceCollection AddOtpAndCache(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<OtpSettings>(config.GetSection("OtpSettings"));

        var redisConnectionString = config["REDIS:ConnectionString"] ?? config.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "Cargo_OTP_Cache_";
        });

        services.AddSingleton<ICacheService, CacheService>();
        services.AddTransient<IOtpService, OtpService>();
        return services;
    }

    // 3. Install Notification Outbox
    //    TOutboxDbContext must implement IOutboxDbContext (i.e. expose DbSet<OutboxMessage>).
    //    Each service calls this once, passing its own DbContext type so the background
    //    worker can resolve it from DI without knowing the concrete type at compile time.
    public static IServiceCollection AddNotificationOutbox<TOutboxDbContext>(
        this IServiceCollection services, IConfiguration config)
        where TOutboxDbContext : class, IOutboxDbContext
    {
        services.Configure<RabbitMqSettings>(config.GetSection(RabbitMqSettings.SectionName));

        // Map the concrete DbContext to the shared outbox interface so DI can resolve it.
        services.AddScoped<IOutboxDbContext>(sp => sp.GetRequiredService<TOutboxDbContext>());

        // Scoped publisher — shares the same DbContext instance as the calling handler.
        services.AddScoped<INotificationPublisher, OutboxNotificationPublisher>();

        // Background worker — uses IServiceScopeFactory to create its own scope per poll.
        services.AddHostedService<OutboxPublisherWorker>();

        return services;
    }

    // 4. Install Storage (S3)
    public static IServiceCollection AddStorageService(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<StorageSettings>(config.GetSection("Storage"));
        services.AddSingleton<IStorageService, StorageService>();
        return services;
    }
}