namespace Cuadre.Api.Endpoints.ExchangeRates;

public static class ExchangeRateEndpointExtensions
{
    public static IEndpointRouteBuilder MapExchangeRateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGetAllExchangeRates();
        app.MapGetCurrentExchangeRate();
        app.MapGetExchangeRate();
        app.MapUpsertExchangeRate();
        return app;
    }
}
