using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.PriceLists;

public static class GetPriceListEndpoint
{
    private const string Name = "GetPriceList";

    public static IEndpointRouteBuilder MapGetPriceList(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.PriceList.Get, async (
                IPriceListService priceListService,
                CancellationToken token) =>
            {
                var priceList = await priceListService.GetAsync(token);
                return TypedResults.Ok(priceList.MapToResponse());
            })
            .WithName(Name)
            .Produces<PriceListResponse>();

        return app;
    }
}
