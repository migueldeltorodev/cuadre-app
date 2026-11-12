using System.Reflection;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.App;

public static class GetAppInfoEndpoint
{
    public const string Name = "GetAppInfo";

    public static IEndpointRouteBuilder MapGetAppInfo(this IEndpointRouteBuilder app)
    {
        // No service/repository: it reads nothing from the database, only the running assembly.
        app.MapGet(ApiEndpoints.App.Info, (IHostEnvironment environment) =>
            {
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

                return TypedResults.Ok(new AppInfoResponse
                {
                    Name = "Cuadre",
                    Version = version,
                    Environment = environment.EnvironmentName
                });
            })
            .WithName(Name)
            .Produces<AppInfoResponse>();

        return app;
    }
}
