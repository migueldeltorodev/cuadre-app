using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Suppliers;

public static class GetSupplierProductsEndpoint
{
    private const string Name = "GetSupplierProducts";

    public static IEndpointRouteBuilder MapGetSupplierProducts(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Suppliers.Products, async (
                Guid id,
                IPurchaseService purchaseService,
                CancellationToken token) =>
            {
                var result = await purchaseService.GetSupplierProductsAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<SupplierProductsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
