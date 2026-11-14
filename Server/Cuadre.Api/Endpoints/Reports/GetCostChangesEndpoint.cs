using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Reports;

public static class GetCostChangesEndpoint
{
    private const string Name = "GetCostChanges";

    public static IEndpointRouteBuilder MapGetCostChanges(this IEndpointRouteBuilder app)
    {
        // ?from=&to=&categoryId=&supplierId= (default: the last 12 months).
        app.MapGet(ApiEndpoints.Reports.CostChanges, async (
                [AsParameters] GetCostChangesRequest request,
                IReportService reportService,
                CancellationToken token) =>
            {
                var report = await reportService.GetCostChangesAsync(request.From, request.To, request.CategoryId,
                    request.SupplierId, token);
                return TypedResults.Ok(report.MapToResponse());
            })
            .WithName(Name)
            .Produces<CostChangesReportResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
