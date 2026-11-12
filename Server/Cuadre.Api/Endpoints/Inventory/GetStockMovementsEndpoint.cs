using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Application.Common;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Inventory;

public static class GetStockMovementsEndpoint
{
    private const string Name = "GetStockMovements";

    public static IEndpointRouteBuilder MapGetStockMovements(this IEndpointRouteBuilder app)
    {
        // ?date=2026-11-12 (default: today) — "Movimientos de hoy".
        app.MapGet(ApiEndpoints.Inventory.Movements, async (
                [AsParameters] GetStockMovementsRequest request,
                IInventoryService inventoryService,
                TimeProvider clock,
                CancellationToken token) =>
            {
                var date = request.Date ?? clock.Today();
                var movements = await inventoryService.GetMovementsOfDayAsync(date, token);
                return TypedResults.Ok(movements.MapToResponse(date));
            })
            .WithName(Name)
            .Produces<StockMovementsResponse>();

        return app;
    }
}
