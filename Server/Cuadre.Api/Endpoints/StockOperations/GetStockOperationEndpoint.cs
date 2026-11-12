using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.StockOperations;

public static class GetStockOperationEndpoint
{
    public const string Name = "GetStockOperation";

    public static IEndpointRouteBuilder MapGetStockOperation(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.StockOperations.Get, async (
                Guid id,
                IStockOperationService stockOperationService,
                CancellationToken token) =>
            {
                var result = await stockOperationService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<StockOperationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
