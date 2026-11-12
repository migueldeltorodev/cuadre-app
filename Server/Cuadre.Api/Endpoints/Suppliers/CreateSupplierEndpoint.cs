using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Suppliers;

public static class CreateSupplierEndpoint
{
    private const string Name = "CreateSupplier";

    public static IEndpointRouteBuilder MapCreateSupplier(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.Suppliers.Create, async (
                CreateSupplierRequest request,
                ISupplierService supplierService,
                CancellationToken token) =>
            {
                var result = await supplierService.CreateAsync(request.MapToSupplier(), token);

                return result.ToCreatedAtRouteResult(
                    x => x.MapToResponse(),
                    GetSupplierEndpoint.Name,
                    x => new { id = x.Id });
            })
            .WithName(Name)
            .Produces<SupplierResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
