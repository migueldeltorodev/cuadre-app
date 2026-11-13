namespace Cuadre.Api.Endpoints.EnvelopeOperations;

/// <summary>Openings, moves between envelopes, withdrawals and tax payments: created, listed and voided.</summary>
public static class EnvelopeOperationEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnvelopeOperationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateEnvelopeOpening();
        app.MapCreateEnvelopeTransfer();
        app.MapCreateEnvelopeWithdrawal();
        app.MapCreateTaxPayment();
        app.MapGetEnvelopeOperation();
        app.MapGetAllEnvelopeOperations();
        app.MapVoidEnvelopeOperation();
        return app;
    }
}
