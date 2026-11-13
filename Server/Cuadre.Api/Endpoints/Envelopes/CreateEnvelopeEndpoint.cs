using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Envelopes;

public static class CreateEnvelopeEndpoint
{
    private const string Name = "CreateEnvelope";

    public static IEndpointRouteBuilder MapCreateEnvelope(this IEndpointRouteBuilder app)
    {
        // A user's own envelope (RF-PF-02). It gets a % only through a new allocation plan.
        app.MapPost(ApiEndpoints.Envelopes.Create, async (
                CreateEnvelopeRequest request,
                IEnvelopeService envelopeService,
                CancellationToken token) =>
            {
                var result = await envelopeService.CreateAsync(request.MapToEnvelope(), token);
                return result.IsSuccess
                    ? Results.Created($"/{ApiEndpoints.Envelopes.GetAll}/{result.Value.Id}", result.Value.MapToResponse())
                    : result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<EnvelopeResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
