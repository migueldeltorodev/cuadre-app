using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.Products;

public static class GetPriceNoticesEndpoint
{
    private const string Name = "GetPriceNotices";

    public static IEndpointRouteBuilder MapGetPriceNotices(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.Products.PriceNotices, async (
                IProductService productService,
                CancellationToken token) =>
            {
                var notices = await productService.GetPriceNoticesAsync(token);
                return TypedResults.Ok(notices.MapToResponse());
            })
            .WithName(Name)
            .Produces<PriceNoticesResponse>();

        return app;
    }
}
