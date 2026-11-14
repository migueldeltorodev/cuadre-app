using Cuadre.Application.Common;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;

namespace Cuadre.Application.Services;

/// <summary>
/// RF-REP-01: the home screen. The envelope balances, the days not closed, the pending shortages and the expenses
/// due already have their own endpoints (the client keeps them in stores), so they are not repeated here.
/// </summary>
public interface IDashboardService
{
    Task<Dashboard> GetAsync(CancellationToken cancellationToken = default);
}

public class DashboardService : IDashboardService
{
    // Every active product at once: the buffer check is pure math per product (no paging on the home screen).
    private const int AllProducts = 100_000;

    private readonly IReportRepository _reportRepository;
    private readonly IEvaluationRepository _evaluationRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBusinessSettingsRepository _settingsRepository;
    private readonly IExchangeRateRepository _exchangeRateRepository;
    private readonly TimeProvider _clock;

    public DashboardService(IReportRepository reportRepository, IEvaluationRepository evaluationRepository,
        IExpenseRepository expenseRepository, IInventoryRepository inventoryRepository,
        IProductRepository productRepository, IBusinessSettingsRepository settingsRepository,
        IExchangeRateRepository exchangeRateRepository, TimeProvider clock)
    {
        _reportRepository = reportRepository;
        _evaluationRepository = evaluationRepository;
        _expenseRepository = expenseRepository;
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _settingsRepository = settingsRepository;
        _exchangeRateRepository = exchangeRateRepository;
        _clock = clock;
    }

    public async Task<Dashboard> GetAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today();
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // The same days of the previous month: 1-12 Nov against 1-12 Oct; 31 Mar against 1-28/29 Feb.
        var previousStart = monthStart.AddMonths(-1);
        var previousEnd = previousStart.AddDays(
            Math.Min(today.Day, DateTime.DaysInMonth(previousStart.Year, previousStart.Month)) - 1);

        var lowStock = (await _inventoryRepository.GetShoppingListAsync(cancellationToken)).ToList();
        var bufferExceeded = await GetBufferExceededAsync(today, cancellationToken);

        return new Dashboard(
            today,
            await _reportRepository.GetLastClosingAsync(cancellationToken),
            await SummarizeAsync(monthStart, today, cancellationToken),
            await SummarizeAsync(previousStart, previousEnd, cancellationToken),
            await _reportRepository.GetDaysAsync(today.AddDays(-(Dashboard.ChartDays - 1)), today, cancellationToken),
            lowStock.Count,
            lowStock.Take(Dashboard.AlertItems).ToList(),
            bufferExceeded.Count,
            bufferExceeded.Take(Dashboard.AlertItems).ToList());
    }

    private async Task<PeriodSummary> SummarizeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var totals = await _evaluationRepository.GetTotalsAsync(from, to, cancellationToken);
        var expenses = await _expenseRepository.GetTotalsAsync(new GetAllExpensesOptions { From = from, To = to },
            cancellationToken);
        return PeriodSummary.Create(from, to, totals, expenses);
    }

    /// <summary>RN-04 for every active product, the furthest beyond its colchón first.</summary>
    private async Task<List<BufferExceededProduct>> GetBufferExceededAsync(DateOnly today,
        CancellationToken cancellationToken)
    {
        var todayRate = (await _exchangeRateRepository.GetLatestOnOrBeforeAsync(today, cancellationToken))
            ?.CupPerUsdCents;
        if (todayRate is null)
        {
            return [];
        }

        var settings = await _settingsRepository.GetAsync(cancellationToken)
                       ?? throw new InvalidOperationException("Business settings are missing.");
        var products = await _productRepository.GetAllAsync(
            new GetAllProductsOptions { Page = 1, PageSize = AllProducts }, cancellationToken);

        return products
            .Select(product => ProductPricingRules.Build(product, settings, todayRate) is { IsBufferExceeded: true } pricing
                ? new BufferExceededProduct(
                    product.Id,
                    product.Code,
                    product.Name,
                    product.SalePriceCents,
                    pricing.SuggestedPriceCents,
                    pricing.CurrentRateCents,
                    pricing.EffectiveBufferCents,
                    todayRate.Value)
                : null)
            .OfType<BufferExceededProduct>()
            .OrderByDescending(x => x.TodayRateCents - x.LastRateCents - x.EffectiveBufferCents)
            .ThenBy(x => x.Name)
            .ToList();
    }
}
