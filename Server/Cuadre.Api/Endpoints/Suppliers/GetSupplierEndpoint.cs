using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Suppliers;

public static class GetSupplierEndpoint
{
    public const string Name = "GetSupplier";

    public static IEndpointRouteBuilder MapGetSupplier(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Suppliers.Get, async (
                Guid id,
                ISupplierService supplierService,
                CancellationToken token) =>
            {
                var result = await supplierService.GetByIdAsync(id, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<SupplierResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
