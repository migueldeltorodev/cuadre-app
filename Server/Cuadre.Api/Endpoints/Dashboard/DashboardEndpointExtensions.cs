namespace Cuadre.Api.Endpoints.Dashboard;

/// <summary>RF-REP-01: the home screen.</summary>
public static class DashboardEndpointExtensions
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetDashboard();
        return app;
    }
}
