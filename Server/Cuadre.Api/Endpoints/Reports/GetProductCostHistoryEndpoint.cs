using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Reports;

public static class GetProductCostHistoryEndpoint
{
    private const string Name = "GetProductCostHistory";

    public static IEndpointRouteBuilder MapGetProductCostHistory(this IEndpointRouteBuilder app)
    {
        // ?from=&to=&supplierId= (default: the whole history, every supplier).
        app.MapGet(ApiEndpoints.Reports.ProductCosts, async (
                Guid id,
                [AsParameters] GetProductCostHistoryRequest request,
                IReportService reportService,
                CancellationToken token) =>
            {
                var result = await reportService.GetProductCostHistoryAsync(id, request.MapToOptions(), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<ProductCostHistoryResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
