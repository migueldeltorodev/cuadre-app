using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.StockOperations;

public static class CreateTransferEndpoint
{
    private const string Name = "CreateTransfer";

    public static IEndpointRouteBuilder MapCreateTransfer(this IEndpointRouteBuilder app)
    {
        // Saving = confirming. A location left below zero is allowed: the response carries a warning.
        app.MapPost(ApiEndpoints.StockOperations.CreateTransfer, async (
                CreateTransferRequest request,
                IStockOperationService stockOperationService,
                CancellationToken token) =>
            {
                var result = await stockOperationService.CreateAsync(request.MapToStockOperation(), token);
                return result.ToCreatedAtRouteResult(
                    x => x.MapToResponse(),
                    GetStockOperationEndpoint.Name,
                    x => new { id = x.Operation.Id });
            })
            .WithName(Name)
            .Produces<StockOperationResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
