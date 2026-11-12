using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Products;

public static class GetProductEndpoint
{
    public const string Name = "GetProduct";

    public static IEndpointRouteBuilder MapGetProduct(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Products.Get, async (
                Guid id,
                IProductService productService,
                CancellationToken token) =>
            {
                var result = await productService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
