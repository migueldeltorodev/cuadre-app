using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.EnvelopeOperations;

public static class GetEnvelopeOperationEndpoint
{
    public const string Name = "GetEnvelopeOperation";

    public static IEndpointRouteBuilder MapGetEnvelopeOperation(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.EnvelopeOperations.Get, async (
                Guid id,
                IEnvelopeOperationService envelopeOperationService,
                CancellationToken token) =>
            {
                var result = await envelopeOperationService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<EnvelopeOperationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
