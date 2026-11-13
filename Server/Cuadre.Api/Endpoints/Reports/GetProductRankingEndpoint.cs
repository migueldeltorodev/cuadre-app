using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Reports;

public static class GetProductRankingEndpoint
{
    private const string Name = "GetProductRanking";

    public static IEndpointRouteBuilder MapGetProductRanking(this IEndpointRouteBuilder app)
    {
        // ?from=&to=&categoryId= (default: this month up to today, every category).
        app.MapGet(ApiEndpoints.Reports.ProductRanking, async (
                [AsParameters] GetProductRankingRequest request,
                IReportService reportService,
                CancellationToken token) =>
            {
                var report = await reportService.GetProductRankingAsync(request.From, request.To, request.CategoryId, token);
                return TypedResults.Ok(report.MapToResponse());
            })
            .WithName(Name)
            .Produces<ProductRankingReportResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
