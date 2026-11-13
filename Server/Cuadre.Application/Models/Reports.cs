namespace Cuadre.Application.Models;

// Slice 8: the dashboard (RF-REP-01) and the reports (RF-REP-02..05, 07, 08). Everything here is READ-ONLY and
// computed from the documents already confirmed: closings, purchases, stock operations, expenses, discrepancies.
// Money in CUP cents unless a currency says otherwise; quantities in thousandths; unit costs in millionths.

/// <summary>One confirmed closing, as the charts need it.</summary>
public class DailyFigures
{
    public Guid ClosingId { get; init; }
    public DateOnly Date { get; init; }
    public long CashCollectedCents { get; init; }
    public long TransferCollectedCents { get; init; }
    public long CostOfSalesCents { get; init; }
    public long TheoreticalSalesCents { get; init; }

    /// <summary>After the tolerance: negative = shortage, positive = surplus.</summary>
    public long AdjustedDifferenceCents { get; init; }

    public long StaffPayCents { get; init; }

    /// <summary>A1.</summary>
    public long CollectedCents => CashCollectedCents + TransferCollectedCents;

    /// <summary>A3.</summary>
    public long RealIncomeCents => CollectedCents - CostOfSalesCents;
}

/// <summary>The totals of a period: what the closings add up to plus the expenses (per currency).</summary>
public sealed record PeriodSummary(
    DateOnly From,
    DateOnly To,
    int ClosedDays,
    long CollectedCents,
    long CostOfSalesCents,
    long TheoreticalSalesCents,
    long StaffPayCents,
    long ShortageCents,
    long SurplusCents,
    IReadOnlyList<ExpenseTotal> Expenses)
{
    /// <summary>A3.</summary>
    public long RealIncomeCents => CollectedCents - CostOfSalesCents;

    public static PeriodSummary Create(DateOnly from, DateOnly to, EvaluationTotals totals,
        IReadOnlyList<ExpenseTotal> expenses) => new(
        from,
        to,
        (int)totals.ClosedDays,
        totals.CashCollectedCents + totals.TransferCollectedCents,
        totals.CostOfSalesCents,
        totals.TheoreticalSalesCents,
        totals.StaffPayCents,
        totals.ShortageCents,
        totals.SurplusCents,
        expenses);
}

/// <summary>A product whose price needs a look: today's rate went beyond its purchase rate + colchón (RN-04).</summary>
public sealed record BufferExceededProduct(
    Guid ProductId,
    string Code,
    string Name,
    long? SalePriceCents,
    long SuggestedPriceCents,
    long AverageRateCents,
    long EffectiveBufferCents,
    long TodayRateCents);

/// <summary>
/// RF-REP-01. The month runs from the 1st to today and is compared with the SAME days of the previous month
/// (1-12 Nov against 1-12 Oct), so a month that just started does not look like a collapse.
/// </summary>
public sealed record Dashboard(
    DateOnly Today,
    DailyFigures? LastClosing,
    PeriodSummary Month,
    PeriodSummary PreviousMonth,
    IReadOnlyList<DailyFigures> Days,
    int LowStockCount,
    IReadOnlyList<ShoppingListItem> LowStock,
    int BufferExceededCount,
    IReadOnlyList<BufferExceededProduct> BufferExceeded)
{
    /// <summary>How many closings the chart covers (back from today).</summary>
    public const int ChartDays = 30;

    /// <summary>How many items each alert lists; the count says how many there are in all.</summary>
    public const int AlertItems = 5;
}

// ---------- Monthly report (RF-REP-05, RF-REP-07) ----------

public class ExpenseCategoryTotal
{
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = "";
    public Currency Currency { get; init; }
    public int Count { get; init; }
    public long AmountCents { get; init; }
}

/// <summary>The cash differences of the period, by the day they happened (voided ones never count).</summary>
public class DiscrepancySummary
{
    public int ShortageCount { get; init; }
    public long ShortageCents { get; init; }
    public int SurplusCount { get; init; }
    public long SurplusCents { get; init; }

    /// <summary>Of those shortages: what came back (on any day), what was written off and what is still open.</summary>
    public long RecoveredCents { get; init; }

    public long LostCents { get; init; }
    public long PendingCents { get; init; }
}

/// <summary>One month of the trend. Expenses per currency: currencies are never added up.</summary>
public sealed record MonthTotals(
    int Year,
    int Month,
    int ClosedDays,
    long CollectedCents,
    long CostOfSalesCents,
    long StaffPayCents,
    long ExpensesCupCents,
    long ExpensesUsdCents)
{
    public long RealIncomeCents => CollectedCents - CostOfSalesCents;
}

/// <summary>
/// The closing of a month: the Profit First evaluation of its days, its expenses, cash differences and losses,
/// every day, and the trend of the last 12 months (this one included) for the month-over-month growth.
/// </summary>
public sealed record MonthlyReport(
    int Year,
    int Month,
    ProfitFirstEvaluation Evaluation,
    IReadOnlyList<ExpenseCategoryTotal> ExpensesByCategory,
    DiscrepancySummary Discrepancies,
    long MerchandiseLossCents,
    IReadOnlyList<DailyFigures> Days,
    IReadOnlyList<MonthTotals> Trend)
{
    public const int TrendMonths = 12;
}

public class GetMonthlyReportOptions
{
    public required int Year { get; init; }
    public required int Month { get; init; }

    /// <summary>To refuse a month that has not started yet.</summary>
    public required DateOnly Today { get; init; }
}

/// <summary>From/To of a report, both inclusive. The service fills the defaults.</summary>
public class ReportPeriodOptions
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
}

// ---------- Losses (RF-REP-08) ----------

/// <summary>Why merchandise was lost. House consumption is an owner's draw (RN-11) and an adjustment a correction.</summary>
public enum LossKind
{
    Shrinkage,
    Expired,
    Damaged,

    /// <summary>A count found less than the stock said.</summary>
    CountDifference
}

/// <summary>A raw line of an exit or a count, as read: the service decides whether it is a loss or a gain.</summary>
public class StockLossRow
{
    public Guid OperationId { get; init; }
    public int OperationNumber { get; init; }
    public DateOnly Date { get; init; }
    public StockOperationKind Kind { get; init; }
    public ExitReason? ExitReason { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public MeasureUnit StockUnit { get; init; }

    /// <summary>Exit: the quantity that left (positive). Count: counted − expected (negative = missing).</summary>
    public long QuantityMilli { get; init; }

    /// <summary>Average CUP cost at that moment. Null when the product was never bought (valued at 0).</summary>
    public long? UnitCostCupMicros { get; init; }
}

public sealed record MerchandiseLoss(
    Guid OperationId,
    int OperationNumber,
    DateOnly Date,
    LossKind Kind,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    MeasureUnit StockUnit,
    long QuantityMilli,
    long? UnitCostCupMicros,
    long CostCents);

public class LostShortage
{
    public Guid DiscrepancyId { get; init; }
    public DateOnly Date { get; init; }
    public long AmountCents { get; init; }
    public long RecoveredCents { get; init; }
    public string? EmployeeName { get; init; }
    public string? Note { get; init; }

    /// <summary>What was finally lost: the shortage minus what came back before writing it off.</summary>
    public long LostCents => AmountCents - RecoveredCents;
}

public sealed record LossReport(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<MerchandiseLoss> Merchandise,
    IReadOnlyList<LostShortage> Shortages,
    long CountGainCents)
{
    public long MerchandiseCents => Merchandise.Sum(x => x.CostCents);
    public long LostShortageCents => Shortages.Sum(x => x.LostCents);
    public long TotalCents => MerchandiseCents + LostShortageCents;

    /// <summary>Loss lines → their cost in cents: qty (thousandths) × cost (millionths), same rounding as A2.</summary>
    public static long CostCents(long quantityMilli, long? unitCostCupMicros) =>
        Units.RoundToLong(quantityMilli * (decimal)(unitCostCupMicros ?? 0) / 10_000_000m);

    /// <summary>
    /// Shrinkage, expired and damaged exits plus the negative differences of the counts are losses (RN-06b).
    /// House consumption and adjustments are not. Positive count differences add up to <c>gainCents</c>.
    /// </summary>
    public static (List<MerchandiseLoss> Losses, long GainCents) Classify(IEnumerable<StockLossRow> rows)
    {
        var losses = new List<MerchandiseLoss>();
        long gain = 0;
        foreach (var row in rows)
        {
            LossKind? kind = row.Kind switch
            {
                StockOperationKind.Count when row.QuantityMilli < 0 => LossKind.CountDifference,
                StockOperationKind.Exit => row.ExitReason switch
                {
                    ExitReason.Shrinkage => LossKind.Shrinkage,
                    ExitReason.Expired => LossKind.Expired,
                    ExitReason.Damaged => LossKind.Damaged,
                    _ => null
                },
                _ => null
            };

            if (row.Kind == StockOperationKind.Count && row.QuantityMilli > 0)
            {
                gain += CostCents(row.QuantityMilli, row.UnitCostCupMicros);
            }

            if (kind is null)
            {
                continue;
            }

            var quantity = Math.Abs(row.QuantityMilli);
            losses.Add(new MerchandiseLoss(row.OperationId, row.OperationNumber, row.Date, kind.Value, row.ProductId,
                row.ProductCode, row.ProductName, row.StockUnit, quantity, row.UnitCostCupMicros,
                CostCents(quantity, row.UnitCostCupMicros)));
        }

        return (losses, gain);
    }
}

// ---------- Product ranking (RF-REP-04) ----------

public class ProductSalesRow
{
    public Guid ProductId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public MeasureUnit StockUnit { get; init; }
    public long SoldMilli { get; init; }
    public long SalesCents { get; init; }
    public long CostCents { get; init; }
}

/// <summary>
/// One product in the period. Sales are the theoretical ones of the closings (sold × price, RN-06): the cash
/// is counted per day, not per product. The client sorts (most sold, most margin, most shrinkage).
/// </summary>
public sealed record ProductRanking(
    Guid ProductId,
    string Code,
    string Name,
    MeasureUnit StockUnit,
    long SoldMilli,
    long SalesCents,
    long CostCents,
    long LossMilli,
    long LossCents)
{
    public long MarginCents => SalesCents - CostCents;

    /// <summary>Margin over sales, in basis points. Null without sales.</summary>
    public int? MarginBps => SalesCents == 0
        ? null
        : (int)Math.Round(MarginCents * 10_000m / SalesCents, MidpointRounding.AwayFromZero);
}

public sealed record ProductRankingReport(DateOnly From, DateOnly To, IReadOnlyList<ProductRanking> Items);

public class GetProductRankingOptions
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }

    /// <summary>A category (with its subcategories) or a subcategory.</summary>
    public Guid? CategoryId { get; init; }
}

// ---------- Costs and prices (RF-REP-02, RF-REP-03) ----------

/// <summary>One confirmed purchase of a product: its landed cost per STOCK unit (goods + share of the charges).</summary>
public class PurchasePoint
{
    public Guid PurchaseId { get; init; }
    public int PurchaseNumber { get; init; }
    public DateOnly Date { get; init; }
    public Guid SupplierId { get; init; }
    public string SupplierName { get; init; } = "";
    public Currency Currency { get; init; }
    public long RateCupPerUsdCents { get; init; }
    public long StockQuantityMilli { get; init; }
    public long LandedUnitCostUsdMicros { get; init; }
    public long LandedUnitCostCupMicros { get; init; }

    // Only filled by the many-products query (cost changes).
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public MeasureUnit StockUnit { get; init; }
    public long? SalePriceCents { get; init; }
}

/// <summary>RF-REP-03: what one supplier charged for the product in the period.</summary>
public sealed record SupplierCost(
    Guid SupplierId,
    string SupplierName,
    int PurchaseCount,
    DateOnly LastDate,
    long LastUnitCostUsdMicros,
    long LastUnitCostCupMicros,
    long MinUnitCostUsdMicros,
    long MaxUnitCostUsdMicros,
    long AverageUnitCostUsdMicros,
    long AverageUnitCostCupMicros,
    long TotalQuantityMilli)
{
    /// <summary>Weighted by quantity: a big purchase weighs more than a small one.</summary>
    public static List<SupplierCost> Summarize(IEnumerable<PurchasePoint> points) => points
        .GroupBy(point => point.SupplierId)
        .Select(group =>
        {
            var ordered = group.OrderBy(x => x.Date).ThenBy(x => x.PurchaseNumber).ToList();
            var last = ordered[^1];
            var quantity = ordered.Sum(x => x.StockQuantityMilli);
            return new SupplierCost(
                group.Key,
                last.SupplierName,
                ordered.Count,
                last.Date,
                last.LandedUnitCostUsdMicros,
                last.LandedUnitCostCupMicros,
                ordered.Min(x => x.LandedUnitCostUsdMicros),
                ordered.Max(x => x.LandedUnitCostUsdMicros),
                WeightedAverage(ordered, x => x.LandedUnitCostUsdMicros),
                WeightedAverage(ordered, x => x.LandedUnitCostCupMicros),
                quantity);
        })
        .OrderBy(x => x.AverageUnitCostUsdMicros)
        .ToList();

    private static long WeightedAverage(IReadOnlyCollection<PurchasePoint> points, Func<PurchasePoint, long> cost)
    {
        var quantity = points.Sum(x => (decimal)x.StockQuantityMilli);
        return quantity == 0
            ? 0
            : Units.RoundToLong(points.Sum(x => x.StockQuantityMilli * (decimal)cost(x)) / quantity);
    }
}

/// <summary>RF-REP-02 for one product: every purchase (cost) and every sale price change in the period.</summary>
public sealed record ProductCostHistory(
    Product Product,
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<PurchasePoint> Purchases,
    IReadOnlyList<ProductPriceChange> PriceChanges,
    IReadOnlyList<SupplierCost> Suppliers);

public class GetProductCostHistoryOptions
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>Only the purchases of this supplier. The supplier comparison always shows all of them.</summary>
    public Guid? SupplierId { get; init; }
}

/// <summary>
/// RF-REP-02 for a category: products with different units cannot be averaged together, so each product shows
/// its first and last cost of the period and the change between them.
/// </summary>
public sealed record ProductCostChange(
    Guid ProductId,
    string Code,
    string Name,
    MeasureUnit StockUnit,
    int PurchaseCount,
    DateOnly FirstDate,
    long FirstUnitCostUsdMicros,
    long FirstUnitCostCupMicros,
    DateOnly LastDate,
    long LastUnitCostUsdMicros,
    long LastUnitCostCupMicros,
    long? SalePriceCents)
{
    /// <summary>Last vs first, in basis points. The USD one is the supplier; the CUP one adds the exchange rate.</summary>
    public int? UsdChangeBps => Change(FirstUnitCostUsdMicros, LastUnitCostUsdMicros);

    public int? CupChangeBps => Change(FirstUnitCostCupMicros, LastUnitCostCupMicros);

    public static List<ProductCostChange> Summarize(IEnumerable<PurchasePoint> points) => points
        .GroupBy(point => point.ProductId)
        .Select(group =>
        {
            var ordered = group.OrderBy(x => x.Date).ThenBy(x => x.PurchaseNumber).ToList();
            var (first, last) = (ordered[0], ordered[^1]);
            return new ProductCostChange(group.Key, last.ProductCode, last.ProductName, last.StockUnit, ordered.Count,
                first.Date, first.LandedUnitCostUsdMicros, first.LandedUnitCostCupMicros,
                last.Date, last.LandedUnitCostUsdMicros, last.LandedUnitCostCupMicros, last.SalePriceCents);
        })
        .OrderByDescending(x => x.CupChangeBps ?? int.MinValue)
        .ThenBy(x => x.Name)
        .ToList();

    private static int? Change(long first, long last) => first == 0
        ? null
        : (int)Math.Round((last - first) * 10_000m / first, MidpointRounding.AwayFromZero);
}

public sealed record CostChangesReport(DateOnly From, DateOnly To, IReadOnlyList<ProductCostChange> Items);

public class GetCostChangesOptions
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? SupplierId { get; init; }
}
