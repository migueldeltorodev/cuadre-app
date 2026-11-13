using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.EnvelopeOperations;

public static class VoidEnvelopeOperationEndpoint
{
    private const string Name = "VoidEnvelopeOperation";

    public static IEndpointRouteBuilder MapVoidEnvelopeOperation(this IEndpointRouteBuilder app)
    {
        // POST, not DELETE: the operation stays, marked as voided, with its reversal movements.
        app.MapPost(ApiEndpoints.EnvelopeOperations.Void, async (
                Guid id,
                VoidEnvelopeOperationRequest request,
                IEnvelopeOperationService envelopeOperationService,
                CancellationToken token) =>
            {
                var result = await envelopeOperationService.VoidAsync(request.MapToVoidEnvelopeOperation(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<EnvelopeOperationResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
