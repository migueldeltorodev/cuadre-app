namespace Cuadre.Api.Endpoints.PriceLists;

/// <summary>The price list to copy by hand to the shop sign (RF-PRD-09).</summary>
public static class PriceListEndpointExtensions
{
    public static IEndpointRouteBuilder MapPriceListEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetPriceList();
        app.MapMarkPriceListCopied();
        return app;
    }
}
