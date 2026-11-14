using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Products;

public static class ResolvePriceNoticeEndpoint
{
    private const string Name = "ResolvePriceNotice";

    public static IEndpointRouteBuilder MapResolvePriceNotice(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.Products.ResolvePriceNotice, async (
                Guid id,
                ResolvePriceNoticeRequest request,
                IProductService productService,
                CancellationToken token) =>
            {
                var result = await productService.ResolvePriceNoticeAsync(
                    request.MapToResolvePriceNotice(id), token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
