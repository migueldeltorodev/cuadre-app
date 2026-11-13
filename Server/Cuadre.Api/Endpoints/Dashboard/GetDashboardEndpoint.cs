using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Dashboard;

public static class GetDashboardEndpoint
{
    private const string Name = "GetDashboard";

    public static IEndpointRouteBuilder MapGetDashboard(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Dashboard.Get, async (
                IDashboardService dashboardService,
                CancellationToken token) =>
            {
                var dashboard = await dashboardService.GetAsync(token);
                return TypedResults.Ok(dashboard.MapToResponse());
            })
            .WithName(Name)
            .Produces<DashboardResponse>();

        return app;
    }
}
