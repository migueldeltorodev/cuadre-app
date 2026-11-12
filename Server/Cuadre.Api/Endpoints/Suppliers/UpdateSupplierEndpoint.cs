using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Suppliers;

public static class UpdateSupplierEndpoint
{
    private const string Name = "UpdateSupplier";

    public static IEndpointRouteBuilder MapUpdateSupplier(this IEndpointRouteBuilder app)
    {
        app.MapPut(ApiEndpoints.Suppliers.Update, async (
                Guid id,
                UpdateSupplierRequest request,
                ISupplierService supplierService,
                CancellationToken token) =>
            {
                var result = await supplierService.UpdateAsync(request.MapToSupplier(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<SupplierResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
