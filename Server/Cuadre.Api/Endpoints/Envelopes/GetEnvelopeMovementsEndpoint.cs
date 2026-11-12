using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Envelopes;

public static class GetEnvelopeMovementsEndpoint
{
    private const string Name = "GetEnvelopeMovements";

    public static IEndpointRouteBuilder MapGetEnvelopeMovements(this IEndpointRouteBuilder app)
    {
        // ?from=...&to=...&cashBoxId=...: the rows of the envelope with the running balance per box.
        app.MapGet(ApiEndpoints.Envelopes.Movements, async (
                Guid id,
                [AsParameters] GetEnvelopeLedgerRequest request,
                IEnvelopeService envelopeService,
                CancellationToken token) =>
            {
                var options = request.MapToOptions(id);
                var result = await envelopeService.GetLedgerAsync(options, token);
                return result.ToOkResult(x => x.MapToResponse(options));
            })
            .WithName(Name)
            .Produces<EnvelopeLedgerResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
