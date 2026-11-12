using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Inventory;

public static class GetStockEndpoint
{
    private const string Name = "GetStock";

    public static IEndpointRouteBuilder MapGetStock(this IEndpointRouteBuilder app)
    {
        // ?search=arroz&categoryId=...&lowOnly=true&includeInactive=true
        app.MapGet(ApiEndpoints.Inventory.Stock, async (
                [AsParameters] GetStockRequest request,
                IInventoryService inventoryService,
                CancellationToken token) =>
            {
                var stock = await inventoryService.GetStockAsync(request.MapToOptions(), token);
                return TypedResults.Ok(stock.MapToResponse());
            })
            .WithName(Name)
            .Produces<StockResponse>();

        return app;
    }
}
