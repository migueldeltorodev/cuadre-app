namespace Cuadre.Api.Endpoints.Inventory;

/// <summary>Read-only views over the stock ledger.</summary>
public static class InventoryEndpointExtensions
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetStock();
        app.MapGetStockMovements();
        app.MapGetShoppingList();
        app.MapGetKardex();
        return app;
    }
}
