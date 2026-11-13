using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.EnvelopeOperations;

public static class CreateEnvelopeTransferEndpoint
{
    private const string Name = "CreateEnvelopeTransfer";

    public static IEndpointRouteBuilder MapCreateEnvelopeTransfer(this IEndpointRouteBuilder app)
    {
        // Movimiento entre sobres, inside one cash box.
        app.MapPost(ApiEndpoints.EnvelopeOperations.CreateTransfer, async (
                CreateEnvelopeTransferRequest request,
                IEnvelopeOperationService envelopeOperationService,
                CancellationToken token) =>
            {
                var result = await envelopeOperationService.CreateAsync(request.MapToEnvelopeOperation(), token);
                return result.ToCreatedAtRouteResult(
                    x => x.MapToResponse(),
                    GetEnvelopeOperationEndpoint.Name,
                    x => new { id = x.Operation.Id });
            })
            .WithName(Name)
            .Produces<EnvelopeOperationResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
