namespace Cuadre.Api.Endpoints.Units;

/// <summary>Read-only list of measure units.</summary>
public static class UnitEndpointExtensions
{
    public static IEndpointRouteBuilder MapUnitEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetAllUnits();
        return app;
    }
}
