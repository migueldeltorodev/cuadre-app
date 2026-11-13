namespace Cuadre.Api.Endpoints.Locations;

/// <summary>No DELETE: a location with history is deactivated (RF-INV-01).</summary>
public static class LocationEndpointExtensions
{
    public static IEndpointRouteBuilder MapLocationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateLocation();
        app.MapGetLocation();
        app.MapGetAllLocations();
        app.MapUpdateLocation();
        return app;
    }
}
