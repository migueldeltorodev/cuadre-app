using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Reports;

public static class GetAccountantSummaryEndpoint
{
    private const string Name = "GetAccountantSummary";

    public static IEndpointRouteBuilder MapGetAccountantSummary(this IEndpointRouteBuilder app)
    {
        // D-63. ?year=2026&month=10 (default: this month up to today).
        app.MapGet(ApiEndpoints.Reports.Accountant, async (
                [AsParameters] GetMonthlyReportRequest request,
                IReportService reportService,
                CancellationToken token) =>
            {
                var summary = await reportService.GetAccountantSummaryAsync(request.Year, request.Month, token);
                return TypedResults.Ok(summary.MapToResponse());
            })
            .WithName(Name)
            .Produces<AccountantSummaryResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
