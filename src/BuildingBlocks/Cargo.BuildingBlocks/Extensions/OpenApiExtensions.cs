using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Cargo.BuildingBlocks.Extensions;

public static class OpenApiExtensions
{
    public static IServiceCollection AddCargoOpenApi(this IServiceCollection services, string title, string version = "v1")
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Info ??= new OpenApiInfo();
                document.Info.Title = title;
                document.Info.Version = version;

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
                var hasAuthorize = metadata?.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any() == true;
                var hasAllowAnonymous = metadata?.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any() == true;

                if (hasAuthorize && !hasAllowAnonymous)
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

    public static WebApplication UseCargoOpenApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        return app;
    }
}