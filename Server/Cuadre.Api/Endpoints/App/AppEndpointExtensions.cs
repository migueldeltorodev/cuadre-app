namespace Cuadre.Api.Endpoints.App;

public static class AppEndpointExtensions
{
    public static IEndpointRouteBuilder MapAppEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetAppInfo();
        return app;
    }
}
