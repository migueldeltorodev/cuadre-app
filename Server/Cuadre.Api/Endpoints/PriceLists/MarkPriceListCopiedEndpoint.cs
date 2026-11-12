using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.PriceLists;

public static class MarkPriceListCopiedEndpoint
{
    private const string Name = "MarkPriceListCopied";

    public static IEndpointRouteBuilder MapMarkPriceListCopied(this IEndpointRouteBuilder app)
    {
        // No body: "I just copied the list to the shop sign". Returns the recorded copy.
        app.MapPost(ApiEndpoints.PriceList.Copies, async (
                IPriceListService priceListService,
                CancellationToken token) =>
            {
                var copy = await priceListService.MarkCopiedAsync(token);
                return TypedResults.Ok(copy.MapToResponse());
            })
            .WithName(Name)
            .Produces<PriceListCopyResponse>();

        return app;
    }
}
