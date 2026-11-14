namespace Cuadre.Api.Endpoints.Taxes;

/// <summary>D-62. No DELETE: a tax is deactivated.</summary>
public static class TaxEndpointExtensions
{
    public static IEndpointRouteBuilder MapTaxEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateTax();
        app.MapGetAllTaxes();
        app.MapUpdateTax();
        return app;
    }
}
