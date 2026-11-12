namespace Cuadre.Api.Endpoints.Envelopes;

/// <summary>Profit First envelopes, their balances and their history.</summary>
public static class EnvelopeEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnvelopeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetAllEnvelopes();
        app.MapCreateEnvelope();
        app.MapUpdateEnvelope();
        app.MapGetEnvelopeBalances();
        app.MapGetEnvelopeMovements();
        return app;
    }
}
