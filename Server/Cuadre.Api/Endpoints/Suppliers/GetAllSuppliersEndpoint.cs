using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Suppliers;

public static class GetAllSuppliersEndpoint
{
    private const string Name = "GetAllSuppliers";

    public static IEndpointRouteBuilder MapGetAllSuppliers(this IEndpointRouteBuilder app)
    {
        // Inactive suppliers too (active first): the UI decides what to show.
        app.MapGet(ApiEndpoints.Suppliers.GetAll, async (
                ISupplierService supplierService,
                CancellationToken token) =>
            {
                var items = await supplierService.GetAllAsync(token);
                return TypedResults.Ok(items.MapToResponse());
            })
            .WithName(Name)
            .Produces<SuppliersResponse>();

        return app;
    }
}
