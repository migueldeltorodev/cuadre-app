using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Reports;

public static class GetMonthlyReportEndpoint
{
    private const string Name = "GetMonthlyReport";

    public static IEndpointRouteBuilder MapGetMonthlyReport(this IEndpointRouteBuilder app)
    {
        // ?year=2026&month=10 (default: this month up to today).
        app.MapGet(ApiEndpoints.Reports.Monthly, async (
                [AsParameters] GetMonthlyReportRequest request,
                IReportService reportService,
                CancellationToken token) =>
            {
                var report = await reportService.GetMonthlyAsync(request.Year, request.Month, token);
                return TypedResults.Ok(report.MapToResponse());
            })
            .WithName(Name)
            .Produces<MonthlyReportResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
