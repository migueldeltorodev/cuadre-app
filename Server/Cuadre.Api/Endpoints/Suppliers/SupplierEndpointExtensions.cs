namespace Cuadre.Api.Endpoints.Suppliers;

/// <summary>No DELETE: suppliers are deactivated (PUT isActive=false), never removed.</summary>
public static class SupplierEndpointExtensions
{
    public static IEndpointRouteBuilder MapSupplierEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateSupplier();
        app.MapGetSupplier();
        app.MapGetAllSuppliers();
        app.MapUpdateSupplier();
        app.MapGetSupplierProducts();
        return app;
    }
}
