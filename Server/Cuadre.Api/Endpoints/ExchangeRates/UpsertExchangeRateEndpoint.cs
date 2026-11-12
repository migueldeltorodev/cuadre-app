using Cuadre.Api.Mapping;
using Cuadre.Application.Services;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Endpoints.ExchangeRates;

public static class UpsertExchangeRateEndpoint
{
    private const string Name = "UpsertExchangeRate";

    public static IEndpointRouteBuilder MapUpsertExchangeRate(this IEndpointRouteBuilder app)
    {
        // PUT is idempotent: typing the rate of the same day again simply corrects it.
        app.MapPut(ApiEndpoints.ExchangeRates.Upsert, async (
                DateOnly date,
                UpsertExchangeRateRequest request,
                IExchangeRateService exchangeRateService,
                CancellationToken token) =>
            {
                var result = await exchangeRateService.UpsertAsync(date, request.CupPerUsdCents, token);
                return result.ToOkResult(x => x.MapToResponse());
            })
            .WithName(Name)
            .Produces<ExchangeRateResponse>()
            .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
