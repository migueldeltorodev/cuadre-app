namespace Cuadre.Api.Endpoints.Reports;

/// <summary>RF-REP-02..05, 07 and 08. Read-only.</summary>
public static class ReportEndpointExtensions
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetMonthlyReport();
        app.MapGetLossReport();
        app.MapGetProductRanking();
        app.MapGetProductCostHistory();
        app.MapGetCostChanges();
        return app;
    }
}
