using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.ExchangeRates;

public static class GetAllExchangeRatesEndpoint
{
    private const string Name = "GetAllExchangeRates";

    public static IEndpointRouteBuilder MapGetAllExchangeRates(this IEndpointRouteBuilder app)
    {
        // History, newest first: ?from=2026-11-01&to=2026-11-30&page=1&pageSize=25
        app.MapGet(ApiEndpoints.ExchangeRates.GetAll, async (
                [AsParameters] GetAllExchangeRatesRequest request,
                IExchangeRateService exchangeRateService,
                CancellationToken token) =>
            {
                var options = request.MapToOptions();
                var rates = await exchangeRateService.GetAllAsync(options, token);
                var total = await exchangeRateService.GetCountAsync(options, token);
                return TypedResults.Ok(rates.MapToResponse(options.Page, options.PageSize, total));
            })
            .WithName(Name)
            .Produces<ExchangeRatesResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest);

        return app;
    }
}
