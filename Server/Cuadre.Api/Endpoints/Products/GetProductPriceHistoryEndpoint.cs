using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Products;

public static class GetProductPriceHistoryEndpoint
{
    private const string Name = "GetProductPriceHistory";

    public static IEndpointRouteBuilder MapGetProductPriceHistory(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Products.PriceHistory, async (
                Guid id,
                IProductService productService,
                CancellationToken token) =>
            {
                var result = await productService.GetPriceHistoryAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<PriceHistoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
