namespace Cuadre.Api.Endpoints.Settings;

public static class SettingsEndpointExtensions
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetBusinessSettings();
        app.MapUpdateBusinessSettings();
        return app;
    }
}
