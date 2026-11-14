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
        services.AddScoped<IStockOperationRepository, StockOperationRepository>();
        services.AddScoped<IStockOperationService, StockOperationService>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IEnvelopeRepository, EnvelopeRepository>();
        services.AddScoped<IEnvelopeService, EnvelopeService>();
        services.AddScoped<IEnvelopeOperationRepository, EnvelopeOperationRepository>();
        services.AddScoped<IEnvelopeOperationService, EnvelopeOperationService>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IDailyClosingRepository, DailyClosingRepository>();
        services.AddScoped<IDailyClosingService, DailyClosingService>();
        services.AddScoped<ICashDiscrepancyRepository, CashDiscrepancyRepository>();
        services.AddScoped<ICashDiscrepancyService, CashDiscrepancyService>();
        services.AddScoped<IEvaluationRepository, EvaluationRepository>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<IExpenseCategoryRepository, ExpenseCategoryRepository>();
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IRecurringExpenseRepository, RecurringExpenseRepository>();
        services.AddScoped<IRecurringExpenseService, RecurringExpenseService>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ICostingRepository, CostingRepository>();

        // RF-CIE-10: every service that dates something asks it before writing.
        services.AddScoped<IClosedDayGuard, ClosedDayGuard>();

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

    /// <summary>
    /// Backups (RF-DAT-01..06). Singletons: one gate for every copy and restore, one queue for the automatic ones.
    /// Needs AddApplication (validators, clock) and AddDatabase (initializer, seeder).
    /// TryAdd for the scheduler: the tests put a recorder in its place.
    /// </summary>
    public static IServiceCollection AddBackups(this IServiceCollection services, BackupPaths paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton<BackupStateStore>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<BackupQueue>();
        services.TryAddSingleton<IBackupScheduler>(provider => provider.GetRequiredService<BackupQueue>());
        return services;
    }
}
