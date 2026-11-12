using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Envelopes;

public static class UpdateEnvelopeEndpoint
{
    private const string Name = "UpdateEnvelope";

    public static IEndpointRouteBuilder MapUpdateEnvelope(this IEndpointRouteBuilder app)
    {
        // Rename or (de)activate. Deactivating needs a Custom envelope, empty and without a %.
        app.MapPut(ApiEndpoints.Envelopes.Update, async (
                Guid id,
                UpdateEnvelopeRequest request,
                IEnvelopeService envelopeService,
                CancellationToken token) =>
            {
                var result = await envelopeService.UpdateAsync(request.MapToEnvelopeChanges(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<EnvelopeResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
