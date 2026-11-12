namespace Cuadre.Api.Endpoints.CashBoxes;

/// <summary>No DELETE: cash boxes are deactivated (PUT isActive=false), never removed.</summary>
public static class CashBoxEndpointExtensions
{
    public static IEndpointRouteBuilder MapCashBoxEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateCashBox();
        app.MapGetCashBox();
        app.MapGetAllCashBoxes();
        app.MapUpdateCashBox();
        return app;
    }
}
