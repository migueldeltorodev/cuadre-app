using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.StockOperations;

public static class CreateExitEndpoint
{
    private const string Name = "CreateExit";

    public static IEndpointRouteBuilder MapCreateExit(this IEndpointRouteBuilder app)
    {
        // Merma, vencido, roto, consumo de la casa, ajuste: valued at the average cost of that moment.
        app.MapPost(ApiEndpoints.StockOperations.CreateExit, async (
                CreateExitRequest request,
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
