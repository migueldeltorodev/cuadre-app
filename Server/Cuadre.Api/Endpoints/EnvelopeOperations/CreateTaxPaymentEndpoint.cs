using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.EnvelopeOperations;

public static class CreateTaxPaymentEndpoint
{
    private const string Name = "CreateTaxPayment";

    public static IEndpointRouteBuilder MapCreateTaxPayment(this IEndpointRouteBuilder app)
    {
        // Pago de impuestos, always from the Impuestos envelope.
        app.MapPost(ApiEndpoints.EnvelopeOperations.CreateTaxPayment, async (
                CreateTaxPaymentRequest request,
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
