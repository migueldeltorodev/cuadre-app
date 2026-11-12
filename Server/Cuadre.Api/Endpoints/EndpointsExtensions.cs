using Cuadre.Api.Endpoints.App;
using Cuadre.Api.Endpoints.CashBoxes;
using Cuadre.Api.Endpoints.Categories;
using Cuadre.Api.Endpoints.ExchangeRates;
using Cuadre.Api.Endpoints.PaymentMethods;
using Cuadre.Api.Endpoints.PriceLists;
using Cuadre.Api.Endpoints.Products;
using Cuadre.Api.Endpoints.Settings;
using Cuadre.Api.Endpoints.Suppliers;
using Cuadre.Api.Endpoints.Units;

namespace Cuadre.Api.Endpoints;

public static class EndpointsExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        // Versioning is applied ONCE to the whole group, so no endpoint can forget it.
        var api = app.NewVersionedApi();
        var v1 = api.MapGroup(string.Empty).HasApiVersion(1.0);

        v1.MapAppEndpoints();
        v1.MapSettingsEndpoints();
        v1.MapCashBoxEndpoints();
        v1.MapPaymentMethodEndpoints();
        v1.MapExchangeRateEndpoints();
        v1.MapCategoryEndpoints();
        v1.MapSupplierEndpoints();
        v1.MapUnitEndpoints();
        v1.MapProductEndpoints();
        v1.MapPriceListEndpoints();

        return app;
    }
}
