namespace Cuadre.Api.Endpoints.Purchases;

/// <summary>No PUT and no DELETE: a confirmed purchase is never edited, only voided (RF-CMP-07).</summary>
public static class PurchaseEndpointExtensions
{
    public static IEndpointRouteBuilder MapPurchaseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreatePurchase();
        app.MapGetPurchase();
        app.MapGetAllPurchases();
        app.MapVoidPurchase();
        return app;
    }
}
