using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Inventory;

public static class GetKardexEndpoint
{
    private const string Name = "GetKardex";

    public static IEndpointRouteBuilder MapGetKardex(this IEndpointRouteBuilder app)
    {
        // ?from=2026-11-01&to=2026-11-30&locationId=...
        app.MapGet(ApiEndpoints.Inventory.Kardex, async (
                Guid id,
                [AsParameters] GetKardexRequest request,
                IInventoryService inventoryService,
                CancellationToken token) =>
            {
                var options = request.MapToOptions(id);
                var result = await inventoryService.GetKardexAsync(options, token);
                return result.ToOkResult(x => x.MapToResponse(options));
            })
            .WithName(Name)
            .Produces<KardexResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
