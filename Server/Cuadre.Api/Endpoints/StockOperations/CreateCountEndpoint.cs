using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.StockOperations;

public static class CreateCountEndpoint
{
    private const string Name = "CreateCount";

    public static IEndpointRouteBuilder MapCreateCount(this IEndpointRouteBuilder app)
    {
        // The server computes what should be there and adjusts the difference (shortage = loss at cost).
        app.MapPost(ApiEndpoints.StockOperations.CreateCount, async (
                CreateCountRequest request,
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
