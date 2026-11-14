using Cuadre.Application.Common;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using Cuadre.Application.Repositories;
using FluentResults;
using FluentValidation;

namespace Cuadre.Application.Services;

/// <summary>Slice 8: the reports. Read-only; without dates they default to this month up to today.</summary>
public interface IReportService
{
    /// <summary>RF-REP-05 and RF-REP-07. Without year/month: this month.</summary>
    Task<MonthlyReport> GetMonthlyAsync(int? year, int? month, CancellationToken cancellationToken = default);

    /// <summary>D-63: the estado de rendimiento of a month for the accountant. Without year/month: this month.</summary>
    Task<AccountantSummary> GetAccountantSummaryAsync(int? year, int? month,
        CancellationToken cancellationToken = default);

    /// <summary>RF-REP-08.</summary>
    Task<LossReport> GetLossesAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    /// <summary>RF-REP-04.</summary>
    Task<ProductRankingReport> GetProductRankingAsync(DateOnly? from, DateOnly? to, Guid? categoryId,
        CancellationToken cancellationToken = default);

    /// <summary>RF-REP-02 and RF-REP-03 for one product. Without dates: its whole history.</summary>
    Task<Result<ProductCostHistory>> GetProductCostHistoryAsync(Guid productId, GetProductCostHistoryOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>RF-REP-02 for many products. Without dates: the last 12 months.</summary>
    Task<CostChangesReport> GetCostChangesAsync(DateOnly? from, DateOnly? to, Guid? categoryId, Guid? supplierId,
        CancellationToken cancellationToken = default);
}

public class ReportService : IReportService
{
    private readonly IReportRepository _repository;
    private readonly IEvaluationService _evaluationService;
    private readonly IProductRepository _productRepository;
    private readonly ITaxRepository _taxRepository;
    private readonly IValidator<GetMonthlyReportOptions> _monthlyValidator;
    private readonly IValidator<ReportPeriodOptions> _periodValidator;
    private readonly IValidator<GetProductRankingOptions> _rankingValidator;
    private readonly IValidator<GetProductCostHistoryOptions> _costHistoryValidator;
    private readonly IValidator<GetCostChangesOptions> _costChangesValidator;
    private readonly TimeProvider _clock;

    public ReportService(IReportRepository repository, IEvaluationService evaluationService,
        IProductRepository productRepository, ITaxRepository taxRepository,
        IValidator<GetMonthlyReportOptions> monthlyValidator,
        IValidator<ReportPeriodOptions> periodValidator, IValidator<GetProductRankingOptions> rankingValidator,
        IValidator<GetProductCostHistoryOptions> costHistoryValidator,
        IValidator<GetCostChangesOptions> costChangesValidator, TimeProvider clock)
    {
        _repository = repository;
        _evaluationService = evaluationService;
        _productRepository = productRepository;
        _taxRepository = taxRepository;
        _monthlyValidator = monthlyValidator;
        _periodValidator = periodValidator;
        _rankingValidator = rankingValidator;
        _costHistoryValidator = costHistoryValidator;
        _costChangesValidator = costChangesValidator;
        _clock = clock;
    }

    public async Task<MonthlyReport> GetMonthlyAsync(int? year, int? month,
        CancellationToken cancellationToken = default)
    {
        var (options, from, to) = await MonthAsync(year, month, cancellationToken);

        var evaluation = await _evaluationService.GetAsync(from, to, cancellationToken);
        var (losses, _) = LossReport.Classify(
            await _repository.GetStockLossRowsAsync(from, to, null, cancellationToken));

        var trendFrom = from.AddMonths(-(MonthlyReport.TrendMonths - 1));
        var found = (await _repository.GetMonthTotalsAsync(trendFrom, to, cancellationToken))
            .ToDictionary(x => (x.Year, x.Month));
        var trend = Enumerable.Range(0, MonthlyReport.TrendMonths)
            .Select(offset => trendFrom.AddMonths(offset))
            .Select(start => found.GetValueOrDefault((start.Year, start.Month))
                             ?? new MonthTotals(start.Year, start.Month, 0, 0, 0, 0, 0, 0))
            .ToList();

        return new MonthlyReport(
            options.Year,
            options.Month,
            evaluation,
            await _repository.GetExpensesByCategoryAsync(from, to, cancellationToken),
            await _repository.GetDiscrepancySummaryAsync(from, to, cancellationToken),
            losses.Sum(x => x.CostCents),
            await _repository.GetDaysAsync(from, to, cancellationToken),
            trend);
    }

    public async Task<AccountantSummary> GetAccountantSummaryAsync(int? year, int? month,
        CancellationToken cancellationToken = default)
    {
        var (options, from, to) = await MonthAsync(year, month, cancellationToken);

        var days = await _repository.GetDaysAsync(from, to, cancellationToken);
        var (losses, countGain) = LossReport.Classify(
            await _repository.GetStockLossRowsAsync(from, to, null, cancellationToken));
        var discrepancies = await _repository.GetDiscrepancySummaryAsync(from, to, cancellationToken);

        // The opening is how the month found the shelves: the end of the day before the 1st.
        var inventory = LocationInventory.Join(
            await _repository.GetInventoryValueAsync(from.AddDays(-1), cancellationToken),
            await _repository.GetInventoryValueAsync(to, cancellationToken));

        return AccountantSummary.Create(
            options.Year,
            options.Month,
            from,
            to,
            days.Count,
            await _repository.GetIncomeByMethodAsync(from, to, cancellationToken),
            await _repository.GetCostOfSalesSplitAsync(from, to, cancellationToken),
            ConvertedExpenseTotal.Summarize(await _repository.GetExpenseLinesAsync(from, to, cancellationToken)),
            days.Sum(day => day.StaffPayCents),
            losses.Sum(loss => loss.CostCents),
            countGain,
            discrepancies.LostCents,
            await _taxRepository.GetAllAsync(cancellationToken),
            PurchaseTotals.Summarize(await _repository.GetPurchaseAmountRowsAsync(from, to, cancellationToken)),
            inventory);
    }

    public async Task<LossReport> GetLossesAsync(DateOnly? from, DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var options = ThisMonthByDefault(from, to);
        await _periodValidator.ValidateOrThrowAsync(options, cancellationToken);

        var (losses, gain) = LossReport.Classify(
            await _repository.GetStockLossRowsAsync(options.From, options.To, null, cancellationToken));
        var shortages = await _repository.GetLostShortagesAsync(options.From, options.To, cancellationToken);
        return new LossReport(options.From, options.To, losses, shortages, gain);
    }

    public async Task<ProductRankingReport> GetProductRankingAsync(DateOnly? from, DateOnly? to, Guid? categoryId,
        CancellationToken cancellationToken = default)
    {
        var period = ThisMonthByDefault(from, to);
        var options = new GetProductRankingOptions { From = period.From, To = period.To, CategoryId = categoryId };
        await _rankingValidator.ValidateOrThrowAsync(options, cancellationToken);

        var sales = await _repository.GetProductSalesAsync(options.From, options.To, categoryId, cancellationToken);
        var (losses, _) = LossReport.Classify(
            await _repository.GetStockLossRowsAsync(options.From, options.To, categoryId, cancellationToken));
        var lossesByProduct = losses.GroupBy(x => x.ProductId).ToDictionary(group => group.Key, group => group.ToList());

        // Sold products, plus the ones that were only lost in the period (most shrinkage).
        var rows = sales
            .Select(sale =>
            {
                var lost = lossesByProduct.GetValueOrDefault(sale.ProductId) ?? [];
                return new ProductRanking(sale.ProductId, sale.Code, sale.Name, sale.StockUnit, sale.SoldMilli,
                    sale.SalesCents, sale.CostCents, lost.Sum(x => x.QuantityMilli), lost.Sum(x => x.CostCents));
            })
            .Concat(lossesByProduct
                .Where(pair => sales.All(sale => sale.ProductId != pair.Key))
                .Select(pair =>
                {
                    var first = pair.Value[0];
                    return new ProductRanking(pair.Key, first.ProductCode, first.ProductName, first.StockUnit, 0, 0,
                        0, pair.Value.Sum(x => x.QuantityMilli), pair.Value.Sum(x => x.CostCents));
                }))
            .OrderByDescending(x => x.SalesCents)
            .ThenByDescending(x => x.LossCents)
            .ThenBy(x => x.Name)
            .ToList();

        return new ProductRankingReport(options.From, options.To, rows);
    }

    public async Task<Result<ProductCostHistory>> GetProductCostHistoryAsync(Guid productId,
        GetProductCostHistoryOptions options, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return Result.Fail(NotFoundError.For<Product>(productId));
        }

        await _costHistoryValidator.ValidateOrThrowAsync(options, cancellationToken);

        // The comparison between suppliers (RF-REP-03) always takes them all; the filter is for the chart.
        var purchases = await _repository.GetPurchasePointsAsync(productId, options.From, options.To,
            cancellationToken);
        var shown = options.SupplierId is { } supplierId
            ? purchases.Where(x => x.SupplierId == supplierId).ToList()
            : purchases;

        return new ProductCostHistory(
            product,
            options.From,
            options.To,
            shown,
            await _repository.GetPriceChangesAsync(productId, options.From, options.To, cancellationToken),
            SupplierCost.Summarize(purchases));
    }

    public async Task<CostChangesReport> GetCostChangesAsync(DateOnly? from, DateOnly? to, Guid? categoryId,
        Guid? supplierId, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today();
        var options = new GetCostChangesOptions
        {
            From = from ?? today.AddYears(-1).AddDays(1),
            To = to ?? today,
            CategoryId = categoryId,
            SupplierId = supplierId
        };
        await _costChangesValidator.ValidateOrThrowAsync(options, cancellationToken);

        var points = await _repository.GetPurchasePointsAsync(options, cancellationToken);
        return new CostChangesReport(options.From, options.To, ProductCostChange.Summarize(points));
    }

    /// <summary>A month, validated. The current one runs up to today; a past one, to its last day.</summary>
    private async Task<(GetMonthlyReportOptions Options, DateOnly From, DateOnly To)> MonthAsync(int? year,
        int? month, CancellationToken cancellationToken)
    {
        var today = _clock.Today();
        var options = new GetMonthlyReportOptions
        {
            Year = year ?? today.Year,
            Month = month ?? today.Month,
            Today = today
        };
        await _monthlyValidator.ValidateOrThrowAsync(options, cancellationToken);

        var from = new DateOnly(options.Year, options.Month, 1);
        var lastDay = from.AddMonths(1).AddDays(-1);
        return (options, from, lastDay < today ? lastDay : today);
    }

    private ReportPeriodOptions ThisMonthByDefault(DateOnly? from, DateOnly? to)
    {
        var today = _clock.Today();
        return new ReportPeriodOptions
        {
            From = from ?? new DateOnly(today.Year, today.Month, 1),
            To = to ?? today
        };
    }
}
