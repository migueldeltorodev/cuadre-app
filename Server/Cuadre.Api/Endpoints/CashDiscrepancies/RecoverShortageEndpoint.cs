using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.CashDiscrepancies;

public static class RecoverShortageEndpoint
{
    private const string Name = "RecoverShortage";

    public static IEndpointRouteBuilder MapRecoverShortage(this IEndpointRouteBuilder app)
    {
        // Returns the shortage updated (its status, what is left, the new recovery in the list).
        app.MapPost(ApiEndpoints.CashDiscrepancies.Recover, async (
                Guid id,
                RecoverShortageRequest request,
                ICashDiscrepancyService cashDiscrepancyService,
                CancellationToken token) =>
            {
                var result = await cashDiscrepancyService.RecoverAsync(request.MapToRecoverShortage(id), token);
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
