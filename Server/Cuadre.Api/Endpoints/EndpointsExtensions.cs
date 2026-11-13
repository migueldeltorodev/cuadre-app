using Cuadre.Api.Endpoints.AllocationPlans;
using Cuadre.Api.Endpoints.App;
using Cuadre.Api.Endpoints.CashBoxes;
using Cuadre.Api.Endpoints.CashDiscrepancies;
using Cuadre.Api.Endpoints.Categories;
using Cuadre.Api.Endpoints.DailyClosings;
using Cuadre.Api.Endpoints.Employees;
using Cuadre.Api.Endpoints.EnvelopeOperations;
using Cuadre.Api.Endpoints.Envelopes;
using Cuadre.Api.Endpoints.ExchangeRates;
using Cuadre.Api.Endpoints.Expenses;
using Cuadre.Api.Endpoints.Inventory;
using Cuadre.Api.Endpoints.Locations;
using Cuadre.Api.Endpoints.PaymentMethods;
using Cuadre.Api.Endpoints.PriceLists;
using Cuadre.Api.Endpoints.ProfitFirst;
using Cuadre.Api.Endpoints.Products;
using Cuadre.Api.Endpoints.Purchases;
using Cuadre.Api.Endpoints.Settings;
using Cuadre.Api.Endpoints.StaffPaySchemes;
using Cuadre.Api.Endpoints.StockOperations;
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
        v1.MapLocationEndpoints();
        v1.MapPurchaseEndpoints();
        v1.MapStockOperationEndpoints();
        v1.MapInventoryEndpoints();
        v1.MapEnvelopeEndpoints();
        v1.MapAllocationPlanEndpoints();
        v1.MapEnvelopeOperationEndpoints();
        v1.MapEmployeeEndpoints();
        v1.MapStaffPaySchemeEndpoints();
        v1.MapDailyClosingEndpoints();
        v1.MapCashDiscrepancyEndpoints();
        v1.MapProfitFirstEndpoints();
        v1.MapExpenseCategoryEndpoints();
        v1.MapExpenseEndpoints();
        v1.MapRecurringExpenseEndpoints();

        return app;
    }
}
