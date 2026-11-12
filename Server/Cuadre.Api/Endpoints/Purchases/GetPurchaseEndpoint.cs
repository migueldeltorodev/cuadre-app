using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Purchases;

public static class GetPurchaseEndpoint
{
    public const string Name = "GetPurchase";

    public static IEndpointRouteBuilder MapGetPurchase(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Purchases.Get, async (
                Guid id,
                IPurchaseService purchaseService,
                CancellationToken token) =>
            {
                var result = await purchaseService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<PurchaseResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
