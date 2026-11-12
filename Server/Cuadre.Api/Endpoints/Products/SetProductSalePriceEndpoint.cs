using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Products;

public static class SetProductSalePriceEndpoint
{
    private const string Name = "SetProductSalePrice";

    public static IEndpointRouteBuilder MapSetProductSalePrice(this IEndpointRouteBuilder app)
    {
        app.MapPut(ApiEndpoints.Products.SalePrice, async (
                Guid id,
                SetSalePriceRequest request,
                IProductService productService,
                CancellationToken token) =>
            {
                var result = await productService.SetSalePriceAsync(request.MapToSetSalePrice(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<ProductResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
