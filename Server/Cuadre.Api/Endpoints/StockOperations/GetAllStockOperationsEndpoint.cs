using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.StockOperations;

public static class GetAllStockOperationsEndpoint
{
    private const string Name = "GetAllStockOperations";

    public static IEndpointRouteBuilder MapGetAllStockOperations(this IEndpointRouteBuilder app)
    {
        // ?kind=Exit&from=2026-11-01&to=2026-11-30&locationId=...&productId=...&includeVoided=true
        app.MapGet(ApiEndpoints.StockOperations.GetAll, async (
                [AsParameters] GetAllStockOperationsRequest request,
                IStockOperationService stockOperationService,
                CancellationToken token) =>
            {
                var options = request.MapToOptions();
                var operations = await stockOperationService.GetAllAsync(options, token);
                var total = await stockOperationService.GetCountAsync(options, token);
                return TypedResults.Ok(operations.MapToResponse(options.Page, options.PageSize, total));
            })
            .WithName(Name)
            .Produces<StockOperationsResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
