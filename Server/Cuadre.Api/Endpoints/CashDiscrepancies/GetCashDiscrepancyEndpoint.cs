using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.CashDiscrepancies;

public static class GetCashDiscrepancyEndpoint
{
    private const string Name = "GetCashDiscrepancy";

    public static IEndpointRouteBuilder MapGetCashDiscrepancy(this IEndpointRouteBuilder app)
    {
        // With its recoveries (voided ones too).
        app.MapGet(ApiEndpoints.CashDiscrepancies.Get, async (
                Guid id,
                ICashDiscrepancyService cashDiscrepancyService,
                CancellationToken token) =>
            {
                var result = await cashDiscrepancyService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<CashDiscrepancyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
