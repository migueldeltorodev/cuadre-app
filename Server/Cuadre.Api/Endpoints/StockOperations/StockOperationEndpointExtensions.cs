namespace Cuadre.Api.Endpoints.StockOperations;

/// <summary>Transfers, exits and counts: created, listed and voided. Never edited.</summary>
public static class StockOperationEndpointExtensions
{
    public static IEndpointRouteBuilder MapStockOperationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateTransfer();
        app.MapCreateExit();
        app.MapCreateCount();
        app.MapGetStockOperation();
        app.MapGetAllStockOperations();
        app.MapVoidStockOperation();
        return app;
    }
}
