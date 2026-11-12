using Cuadre.Application.Database;
using Cuadre.Application.Repositories;
using Cuadre.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cuadre.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // The ONLY clock of the app ("today", timestamps). TryAdd: the tests replace it with a fixed clock.
        services.TryAddSingleton(TimeProvider.System);

        // Services and repositories are SCOPED (one pair per aggregate).
        services.AddScoped<IBusinessSettingsRepository, BusinessSettingsRepository>();
        services.AddScoped<IBusinessSettingsService, BusinessSettingsService>();
        services.AddScoped<ICashBoxRepository, CashBoxRepository>();
        services.AddScoped<ICashBoxService, CashBoxService>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
        services.AddScoped<IExchangeRateService, ExchangeRateService>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IPriceListRepository, PriceListRepository>();
        services.AddScoped<IPriceListService, PriceListService>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<IPurchaseService, PurchaseService>();

        // Validators are SINGLETON: they must stay pure (shape/format rules only, no repositories).
        // Data-dependent rules (uniqueness, existence) live in the service and return ConflictError/NotFoundError.
        services.AddValidatorsFromAssemblyContaining<IApplicationMarker>(ServiceLifetime.Singleton);

        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, string connectionString)
    {
        DapperConfiguration.Configure();

        // Stateless infrastructure => Singleton.
        services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(connectionString));
        services.AddSingleton(_ => new DbInitializer(connectionString));
        services.AddSingleton<DatabaseSeeder>();
        return services;
    }
}
