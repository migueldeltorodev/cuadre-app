using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Products;

public static class GetAllProductsEndpoint
{
    private const string Name = "GetAllProducts";

    public static IEndpointRouteBuilder MapGetAllProducts(this IEndpointRouteBuilder app)
    {
        // ?search=arroz&categoryId=...&includeInactive=true&page=1&pageSize=25
        app.MapGet(ApiEndpoints.Products.GetAll, async (
                [AsParameters] GetAllProductsRequest request,
                IProductService productService,
                CancellationToken token) =>
            {
                var options = request.MapToOptions();
                var products = await productService.GetAllAsync(options, token);
                var total = await productService.GetCountAsync(options, token);
                return TypedResults.Ok(products.MapToResponse(options.Page, options.PageSize, total));
            })
            .WithName(Name)
            .Produces<ProductsResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
