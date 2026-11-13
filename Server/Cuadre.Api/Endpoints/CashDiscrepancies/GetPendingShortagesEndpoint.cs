using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.CashDiscrepancies;

public static class GetPendingShortagesEndpoint
{
    private const string Name = "GetPendingShortages";

    public static IEndpointRouteBuilder MapGetPendingShortages(this IEndpointRouteBuilder app)
    {
        // How many shortages are still to claim and how much is left of them (the menu badge).
        app.MapGet(ApiEndpoints.CashDiscrepancies.PendingSummary, async (
                ICashDiscrepancyService cashDiscrepancyService,
                CancellationToken token) =>
            {
                var totals = await cashDiscrepancyService.GetPendingTotalsAsync(token);
                return TypedResults.Ok(totals.MapToPendingShortagesResponse());
            })
            .WithName(Name)
            .Produces<PendingShortagesResponse>();

        return app;
    }
}
