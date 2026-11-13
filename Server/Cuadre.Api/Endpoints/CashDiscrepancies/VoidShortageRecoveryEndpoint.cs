using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.CashDiscrepancies;

public static class VoidShortageRecoveryEndpoint
{
    private const string Name = "VoidShortageRecovery";

    public static IEndpointRouteBuilder MapVoidShortageRecovery(this IEndpointRouteBuilder app)
    {
        // POST, not DELETE: the recovery stays, voided, with reversals dated today. Returns its shortage.
        app.MapPost(ApiEndpoints.ShortageRecoveries.Void, async (
                Guid id,
                VoidShortageRecoveryRequest request,
                ICashDiscrepancyService cashDiscrepancyService,
                CancellationToken token) =>
            {
                var result = await cashDiscrepancyService.VoidRecoveryAsync(
                    request.MapToVoidShortageRecovery(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<CashDiscrepancyResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
