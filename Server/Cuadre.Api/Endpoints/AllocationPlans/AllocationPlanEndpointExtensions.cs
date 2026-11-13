namespace Cuadre.Api.Endpoints.AllocationPlans;

/// <summary>The % of A3 for each envelope, with a start date.</summary>
public static class AllocationPlanEndpointExtensions
{
    public static IEndpointRouteBuilder MapAllocationPlanEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetAllAllocationPlans();
        app.MapCreateAllocationPlan();
        return app;
    }
}
