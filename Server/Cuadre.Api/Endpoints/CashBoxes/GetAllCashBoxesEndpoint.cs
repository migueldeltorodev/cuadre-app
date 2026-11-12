using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.CashBoxes;

public static class GetAllCashBoxesEndpoint
{
    private const string Name = "GetAllCashBoxes";

    public static IEndpointRouteBuilder MapGetAllCashBoxes(this IEndpointRouteBuilder app)
    {
        // Returns inactive boxes too (active first): the UI decides what to show.
        app.MapGet(ApiEndpoints.CashBoxes.GetAll, async (
                ICashBoxService cashBoxService,
                CancellationToken token) =>
            {
                var cashBoxes = await cashBoxService.GetAllAsync(token);
                return TypedResults.Ok(cashBoxes.MapToResponse());
            })
            .WithName(Name)
            .Produces<CashBoxesResponse>();

        return app;
    }
}
