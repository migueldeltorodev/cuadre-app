using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.ProfitFirst;

public static class GetProfitFirstEvaluationEndpoint
{
    private const string Name = "GetProfitFirstEvaluation";

    public static IEndpointRouteBuilder MapGetProfitFirstEvaluation(this IEndpointRouteBuilder app)
    {
        // ?from=2026-11-01&to=2026-11-30 (default: this month up to today).
        app.MapGet(ApiEndpoints.ProfitFirst.Evaluation, async (
                [AsParameters] GetProfitFirstEvaluationRequest request,
                IEvaluationService evaluationService,
                CancellationToken token) =>
            {
                var evaluation = await evaluationService.GetAsync(request.From, request.To, token);
                return TypedResults.Ok(evaluation.MapToResponse());
            })
            .WithName(Name)
            .Produces<ProfitFirstEvaluationResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
