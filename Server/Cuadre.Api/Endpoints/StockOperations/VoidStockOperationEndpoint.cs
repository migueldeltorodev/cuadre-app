using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.StockOperations;

public static class VoidStockOperationEndpoint
{
    private const string Name = "VoidStockOperation";

    public static IEndpointRouteBuilder MapVoidStockOperation(this IEndpointRouteBuilder app)
    {
        // POST, not DELETE: the operation stays, marked as voided, with its reversal movements.
        app.MapPost(ApiEndpoints.StockOperations.Void, async (
                Guid id,
                VoidStockOperationRequest request,
                IStockOperationService stockOperationService,
                CancellationToken token) =>
            {
                var result = await stockOperationService.VoidAsync(request.MapToVoidStockOperation(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<StockOperationResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
