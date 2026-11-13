using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.CashDiscrepancies;

public static class GetAllCashDiscrepanciesEndpoint
{
    private const string Name = "GetAllCashDiscrepancies";

    public static IEndpointRouteBuilder MapGetAllCashDiscrepancies(this IEndpointRouteBuilder app)
    {
        // ?kind=Shortage&status=Pending&employeeId=...&from=...&to=... Newest first.
        app.MapGet(ApiEndpoints.CashDiscrepancies.GetAll, async (
                [AsParameters] GetAllCashDiscrepanciesRequest request,
                ICashDiscrepancyService cashDiscrepancyService,
                CancellationToken token) =>
            {
                var options = request.MapToOptions();
                var discrepancies = await cashDiscrepancyService.GetAllAsync(options, token);
                var total = await cashDiscrepancyService.GetCountAsync(options, token);
                return TypedResults.Ok(discrepancies.MapToResponse(options.Page, options.PageSize, total));
            })
            .WithName(Name)
            .Produces<CashDiscrepanciesResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
