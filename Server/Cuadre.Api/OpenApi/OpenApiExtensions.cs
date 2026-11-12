using Asp.Versioning;
using Scalar.AspNetCore;

namespace Cuadre.Api.OpenApi;

/// <summary>
/// Built-in OpenAPI (Microsoft.AspNetCore.OpenApi) + Asp.Versioning.OpenApi + Scalar UI.
/// One OpenAPI document per API version (/openapi/v1.json). The Angular client types are
/// generated from that document, so frontend and backend never drift apart.
/// </summary>
public static class OpenApiExtensions
{
    public static IApiVersioningBuilder AddApiDocumentation(this IApiVersioningBuilder builder)
    {
        builder.AddOpenApi();
        return builder;
    }

    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi().WithDocumentPerVersion();

        app.MapScalarApiReference(options =>
        {
            options.WithTitle("Cuadre API");
            options.AddDocuments(app.DescribeApiVersions().Select(d => d.GroupName));
        });

        return app;
    }
}
