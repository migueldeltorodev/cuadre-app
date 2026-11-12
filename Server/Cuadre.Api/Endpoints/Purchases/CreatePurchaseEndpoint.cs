using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Purchases;

public static class CreatePurchaseEndpoint
{
    private const string Name = "CreatePurchase";

    public static IEndpointRouteBuilder MapCreatePurchase(this IEndpointRouteBuilder app)
    {
        // Saving = confirming: stock, cash and costs move in the same transaction.
        app.MapPost(ApiEndpoints.Purchases.Create, async (
                CreatePurchaseRequest request,
                IPurchaseService purchaseService,
                CancellationToken token) =>
            {
                var result = await purchaseService.CreateAsync(request.MapToPurchase(), token);

                return result.ToCreatedAtRouteResult(
                    x => x.MapToResponse(),
                    GetPurchaseEndpoint.Name,
                    x => new { id = x.Purchase.Id });
            })
            .WithName(Name)
            .Produces<PurchaseResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
