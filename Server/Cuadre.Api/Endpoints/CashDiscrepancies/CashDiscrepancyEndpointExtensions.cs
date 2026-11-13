namespace Cuadre.Api.Endpoints.CashDiscrepancies;

/// <summary>Shortages and surpluses: list, detail, recover, write off, void a recovery.</summary>
public static class CashDiscrepancyEndpointExtensions
{
    public static IEndpointRouteBuilder MapCashDiscrepancyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetAllCashDiscrepancies();
        app.MapGetPendingShortages();
        app.MapGetCashDiscrepancy();
        app.MapRecoverShortage();
        app.MapWriteOffShortage();
        app.MapVoidShortageRecovery();
        return app;
    }
}
