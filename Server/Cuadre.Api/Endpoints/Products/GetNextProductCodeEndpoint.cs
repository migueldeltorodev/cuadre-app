using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Products;

public static class GetNextProductCodeEndpoint
{
    private const string Name = "GetNextProductCode";

    public static IEndpointRouteBuilder MapGetNextProductCode(this IEndpointRouteBuilder app)
    {
        // ?categoryId=... -> { "code": "ARR-004" } to prefill the "new product" form.
        app.MapGet(ApiEndpoints.Products.NextCode, async (
                Guid categoryId,
                IProductService productService,
                CancellationToken token) =>
            {
                var result = await productService.GetNextCodeAsync(categoryId, token);
                return result.ToOkResult(code => new NextProductCodeResponse { Code = code });
            })
            .WithName(Name)
            .Produces<NextProductCodeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
