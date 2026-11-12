namespace Cuadre.Api.Endpoints.Locations;

public static class LocationEndpointExtensions
{
    public static IEndpointRouteBuilder MapLocationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetAllLocations();
        return app;
    }
}
