using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Purchases;

public static class VoidPurchaseEndpoint
{
    private const string Name = "VoidPurchase";

    public static IEndpointRouteBuilder MapVoidPurchase(this IEndpointRouteBuilder app)
    {
        // POST, not DELETE: the purchase stays, marked as voided, with its reversal movements.
        app.MapPost(ApiEndpoints.Purchases.Void, async (
                Guid id,
                VoidPurchaseRequest request,
                IPurchaseService purchaseService,
                CancellationToken token) =>
            {
                var result = await purchaseService.VoidAsync(request.MapToVoidPurchase(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<PurchaseResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
