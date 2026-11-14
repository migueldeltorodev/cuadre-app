using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Reports;

public static class GetLossReportEndpoint
{
    private const string Name = "GetLossReport";

    public static IEndpointRouteBuilder MapGetLossReport(this IEndpointRouteBuilder app)
    {
        // ?from=2026-11-01&to=2026-11-30 (default: this month up to today).
        app.MapGet(ApiEndpoints.Reports.Losses, async (
                [AsParameters] GetLossReportRequest request,
                IReportService reportService,
                CancellationToken token) =>
            {
                var report = await reportService.GetLossesAsync(request.From, request.To, token);
                return TypedResults.Ok(report.MapToResponse());
            })
            .WithName(Name)
            .Produces<LossReportResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
