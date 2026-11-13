using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Envelopes;

public static class GetEnvelopeBalancesEndpoint
{
    private const string Name = "GetEnvelopeBalances";

    public static IEndpointRouteBuilder MapGetEnvelopeBalances(this IEndpointRouteBuilder app)
    {
        // ?date=2026-11-12 (default today): envelopes x cash boxes, with the % in force (RF-PF-06, RF-PF-11).
        app.MapGet(ApiEndpoints.Envelopes.Balances, async (
                [AsParameters] GetEnvelopeOverviewRequest request,
                IEnvelopeService envelopeService,
                CancellationToken token) =>
            {
                var overview = await envelopeService.GetOverviewAsync(request.Date, token);
                return TypedResults.Ok(overview.MapToResponse());
            })
            .WithName(Name)
            .Produces<EnvelopeOverviewResponse>();

        return app;
    }
}
