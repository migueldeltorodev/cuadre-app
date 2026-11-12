using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.ExchangeRates;

public static class GetExchangeRateEndpoint
{
    public const string Name = "GetExchangeRate";

    public static IEndpointRouteBuilder MapGetExchangeRate(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiEndpoints.ExchangeRates.Get, async (
                DateOnly date,
                IExchangeRateService exchangeRateService,
                CancellationToken token) =>
            {
                var result = await exchangeRateService.GetByDateAsync(date, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<ExchangeRateResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
