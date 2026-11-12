namespace Cuadre.Api.Endpoints.Products;

/// <summary>No DELETE: products are deactivated (RF-PRD-07), their history must survive.</summary>
public static class ProductEndpointExtensions
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateProduct();
        app.MapGetNextProductCode();
        app.MapGetProduct();
        app.MapGetAllProducts();
        app.MapUpdateProduct();
        app.MapGetProductPriceHistory();
        return app;
    }
}
