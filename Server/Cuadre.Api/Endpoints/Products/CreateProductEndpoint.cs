using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Products;

public static class CreateProductEndpoint
{
    private const string Name = "CreateProduct";

    public static IEndpointRouteBuilder MapCreateProduct(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.Products.Create, async (
                CreateProductRequest request,
                IProductService productService,
                CancellationToken token) =>
            {
                var result = await productService.CreateAsync(request.MapToProduct(), token);

                return result.ToCreatedAtRouteResult(
                    x => x.MapToResponse(),
                    GetProductEndpoint.Name,
                    x => new { id = x.Id });
            })
            .WithName(Name)
            .Produces<ProductResponse>(StatusCodes.Status201Created)
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
