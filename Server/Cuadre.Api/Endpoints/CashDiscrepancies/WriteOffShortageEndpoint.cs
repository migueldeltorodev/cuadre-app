using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.CashDiscrepancies;

public static class WriteOffShortageEndpoint
{
    private const string Name = "WriteOffShortage";

    public static IEndpointRouteBuilder MapWriteOffShortage(this IEndpointRouteBuilder app)
    {
        // What was not recovered is lost. No money moves: the shortage already lowered A1 that day.
        app.MapPost(ApiEndpoints.CashDiscrepancies.WriteOff, async (
                Guid id,
                WriteOffShortageRequest request,
                ICashDiscrepancyService cashDiscrepancyService,
                CancellationToken token) =>
            {
                var result = await cashDiscrepancyService.WriteOffAsync(request.MapToWriteOffShortage(id), token);
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
