using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.AllocationPlans;

public static class CreateAllocationPlanEndpoint
{
    private const string Name = "CreateAllocationPlan";

    public static IEndpointRouteBuilder MapCreateAllocationPlan(this IEndpointRouteBuilder app)
    {
        // Same start date as the latest plan = it is replaced (fixing a typo); an earlier one is refused (422).
        app.MapPost(ApiEndpoints.AllocationPlans.Create, async (
                CreateAllocationPlanRequest request,
                IEnvelopeService envelopeService,
                CancellationToken token) =>
            {
                var result = await envelopeService.SavePlanAsync(request.MapToAllocationPlan(), token);
                return result.IsSuccess
                    ? Results.Created($"/{ApiEndpoints.AllocationPlans.GetAll}", result.Value.MapToResponse())
                    : result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<AllocationPlanResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
