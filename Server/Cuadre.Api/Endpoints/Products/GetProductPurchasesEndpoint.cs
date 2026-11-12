using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Products;

public static class GetProductPurchasesEndpoint
{
    private const string Name = "GetProductPurchases";

    public static IEndpointRouteBuilder MapGetProductPurchases(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Products.Purchases, async (
                Guid id,
                IPurchaseService purchaseService,
                CancellationToken token) =>
            {
                var result = await purchaseService.GetProductPurchasesAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<ProductPurchasesResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
