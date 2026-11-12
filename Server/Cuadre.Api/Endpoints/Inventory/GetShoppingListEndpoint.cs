using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Inventory;

public static class GetShoppingListEndpoint
{
    private const string Name = "GetShoppingList";

    public static IEndpointRouteBuilder MapGetShoppingList(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Inventory.ShoppingList, async (
                IInventoryService inventoryService,
                CancellationToken token) =>
            {
                var items = await inventoryService.GetShoppingListAsync(token);
                return TypedResults.Ok(items.MapToResponse());
            })
            .WithName(Name)
            .Produces<ShoppingListResponse>();

        return app;
    }
}
