using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.EnvelopeOperations;

public static class CreateEnvelopeWithdrawalEndpoint
{
    private const string Name = "CreateEnvelopeWithdrawal";

    public static IEndpointRouteBuilder MapCreateEnvelopeWithdrawal(this IEndpointRouteBuilder app)
    {
        // Retiro: the owner's pay, the profit... Leaving an envelope negative is allowed, with a warning.
        app.MapPost(ApiEndpoints.EnvelopeOperations.CreateWithdrawal, async (
                CreateEnvelopeWithdrawalRequest request,
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
