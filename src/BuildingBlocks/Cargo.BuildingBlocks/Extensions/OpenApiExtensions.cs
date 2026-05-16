using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Cargo.BuildingBlocks.Extensions;

public static class OpenApiExtensions
{
    public static IServiceCollection AddCargoOpenApi(this IServiceCollection services, string title, string servicePrefix, string version = "v1")
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Info ??= new OpenApiInfo();
                document.Info.Title = title;
                document.Info.Version = version;

                // Point Scalar's "Try It" feature at the public gateway path
                // so the browser doesn't try to reach the internal container URL.
                document.Servers =
                [
                    new OpenApiServer { Url = $"/api/{servicePrefix}" }
                ];

                var bearerScheme = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Paste your access_token here. Get one from Auth Endpoints."
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = bearerScheme;

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, ct) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var hasAuthorize  = metadata?.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any() == true;
                var hasAnonymous  = metadata?.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any() == true;

                if (hasAuthorize && !hasAnonymous)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new()
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", new())] = []
                    });
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>
    /// Maps /openapi/v1.json and the Scalar UI for this service.
    /// The UI is configured to fetch its OpenAPI spec via the gateway at
    /// /docs/{servicePrefix}/openapi/v1.json so it works correctly when
    /// accessed through the YARP reverse proxy.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="servicePrefix">
    /// The gateway path segment for this service, e.g. "customer" or "driver".
    /// Must match the prefix used in the gateway's appsettings.json routes.
    /// </param>
    public static WebApplication UseCargoOpenApi(this WebApplication app, string servicePrefix)
    {
        // Serves the raw OpenAPI document at /openapi/v1.json (internal path,
        // proxied by the gateway from /docs/{servicePrefix}/openapi/v1.json).
        app.MapOpenApi();

        // The UI lives at /scalar/v1 on the service (proxied from /docs/{servicePrefix}).
        // WithOpenApiRoutePattern points Scalar's JS fetch to the gateway-level path
        // so the browser resolves it correctly regardless of how the page was accessed.
        app.MapScalarApiReference(options =>
        {
            options.WithOpenApiRoutePattern($"/docs/{servicePrefix}/openapi/v1.json");
        });

        return app;
    }
}