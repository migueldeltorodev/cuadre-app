using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.AllocationPlans;

public static class GetAllAllocationPlansEndpoint
{
    private const string Name = "GetAllAllocationPlans";

    public static IEndpointRouteBuilder MapGetAllAllocationPlans(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.AllocationPlans.GetAll, async (
                IEnvelopeService envelopeService,
                CancellationToken token) =>
            {
                var plans = await envelopeService.GetPlansAsync(token);
                return TypedResults.Ok(plans.MapToResponse());
            })
            .WithName(Name)
            .Produces<AllocationPlansResponse>();

        return app;
    }
}
