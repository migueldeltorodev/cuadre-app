using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.ExchangeRates;

public static class GetCurrentExchangeRateEndpoint
{
    private const string Name = "GetCurrentExchangeRate";

    public static IEndpointRouteBuilder MapGetCurrentExchangeRate(this IEndpointRouteBuilder app)
    {
        // Always 200: "no rate today" is information for the UI warning (RF-MON-03), not an error.
        app.MapGet(ApiEndpoints.ExchangeRates.Current, async (
                IExchangeRateService exchangeRateService,
                CancellationToken token) =>
            {
                var current = await exchangeRateService.GetCurrentAsync(token);
                return TypedResults.Ok(current.MapToResponse());
            })
            .WithName(Name)
            .Produces<CurrentExchangeRateResponse>();

        return app;
    }
}
