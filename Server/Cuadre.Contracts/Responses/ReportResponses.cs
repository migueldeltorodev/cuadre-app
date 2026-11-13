using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

// Slice 8: the dashboard and the reports. Money in CUP cents unless a currency says otherwise; quantities in
// thousandths of the stock unit; unit costs in millionths; percentages in basis points.

/// <summary>One confirmed closing, for the charts.</summary>
public class DailyFiguresResponse
{
    public required Guid ClosingId { get; init; }
    public required DateOnly Date { get; init; }

    /// <summary>A1, A2 and A3.</summary>
    public required long CollectedCents { get; init; }

    public required long CostOfSalesCents { get; init; }
    public required long RealIncomeCents { get; init; }
    public required long TheoreticalSalesCents { get; init; }

    /// <summary>After the tolerance: negative = shortage, positive = surplus.</summary>
    public required long DifferenceCents { get; init; }

    public required long StaffPayCents { get; init; }
}

public class PeriodSummaryResponse
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required int ClosedDays { get; init; }
    public required long CollectedCents { get; init; }
    public required long CostOfSalesCents { get; init; }
    public required long RealIncomeCents { get; init; }
    public required long TheoreticalSalesCents { get; init; }
    public required long StaffPayCents { get; init; }
    public required long ShortageCents { get; init; }
    public required long SurplusCents { get; init; }

    /// <summary>Per currency: currencies are never added up.</summary>
    public required IReadOnlyList<ExpenseTotalResponse> Expenses { get; init; } = [];
}

public class BufferExceededProductResponse
{
    public required Guid ProductId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public long? SalePriceCents { get; init; }
    public required long SuggestedPriceCents { get; init; }

    /// <summary>The rate its stock was bought at, its colchón and today's rate, in CUP cents per USD.</summary>
    public required long AverageRateCents { get; init; }

    public required long BufferCents { get; init; }
    public required long TodayRateCents { get; init; }
}

/// <summary>RF-REP-01.</summary>
public class DashboardResponse
{
    public required DateOnly Today { get; init; }

    /// <summary>The latest confirmed closing (today's, if it is closed). Null before the first one.</summary>
    public DailyFiguresResponse? LastClosing { get; init; }

    /// <summary>From the 1st to today.</summary>
    public required PeriodSummaryResponse Month { get; init; }

    /// <summary>The same days of the previous month, to compare with.</summary>
    public required PeriodSummaryResponse PreviousMonth { get; init; }

    /// <summary>The closings of the last 30 days, oldest first.</summary>
    public required IReadOnlyList<DailyFiguresResponse> Days { get; init; } = [];

    public required int LowStockCount { get; init; }

    /// <summary>The first few; the count says how many there are.</summary>
    public required IReadOnlyList<ShoppingListItemResponse> LowStock { get; init; } = [];

    public required int BufferExceededCount { get; init; }

    /// <summary>The furthest beyond its colchón first.</summary>
    public required IReadOnlyList<BufferExceededProductResponse> BufferExceeded { get; init; } = [];
}

// ---------- Monthly report ----------

public class ExpenseCategoryTotalResponse
{
    public required Guid CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required int Count { get; init; }
    public required long AmountCents { get; init; }
}

/// <summary>The cash differences of the month, by the day they happened.</summary>
public class DiscrepancySummaryResponse
{
    public required int ShortageCount { get; init; }
    public required long ShortageCents { get; init; }
    public required int SurplusCount { get; init; }
    public required long SurplusCents { get; init; }

    /// <summary>Of those shortages: recovered (on any day), written off, still open.</summary>
    public required long RecoveredCents { get; init; }

    public required long LostCents { get; init; }
    public required long PendingCents { get; init; }
}

public class MonthTotalsResponse
{
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required int ClosedDays { get; init; }
    public required long CollectedCents { get; init; }
    public required long CostOfSalesCents { get; init; }
    public required long RealIncomeCents { get; init; }
    public required long StaffPayCents { get; init; }
    public required long ExpensesCupCents { get; init; }
    public required long ExpensesUsdCents { get; init; }
}

/// <summary>RF-REP-05 and RF-REP-07.</summary>
public class MonthlyReportResponse
{
    public required int Year { get; init; }
    public required int Month { get; init; }

    /// <summary>A1, A2, A3, staff, the envelopes against the plan and the expenses of the month (up to today).</summary>
    public required ProfitFirstEvaluationResponse Evaluation { get; init; }

    public required IReadOnlyList<ExpenseCategoryTotalResponse> ExpensesByCategory { get; init; } = [];
    public required DiscrepancySummaryResponse Discrepancies { get; init; }

    /// <summary>Registered shrinkage at cost (RF-REP-08). The detail is in the losses report.</summary>
    public required long MerchandiseLossCents { get; init; }

    public required IReadOnlyList<DailyFiguresResponse> Days { get; init; } = [];

    /// <summary>The last 12 months, this one last (months without data in zero).</summary>
    public required IReadOnlyList<MonthTotalsResponse> Trend { get; init; } = [];
}

// ---------- Losses ----------

public class MerchandiseLossResponse
{
    public required Guid OperationId { get; init; }
    public required int OperationNumber { get; init; }
    public required DateOnly Date { get; init; }
    public required LossType Kind { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductCode { get; init; }
    public required string ProductName { get; init; }
    public required UnitCode StockUnit { get; init; }

    /// <summary>What was lost (always positive).</summary>
    public required long QuantityMilli { get; init; }

    /// <summary>Null when the product was never bought: it is valued at 0.</summary>
    public long? UnitCostCupMicros { get; init; }

    public required long CostCents { get; init; }
}

public class LostShortageResponse
{
    public required Guid DiscrepancyId { get; init; }
    public required DateOnly Date { get; init; }
    public required long AmountCents { get; init; }
    public required long RecoveredCents { get; init; }
    public required long LostCents { get; init; }
    public string? EmployeeName { get; init; }
    public string? Note { get; init; }
}

public class LossTypeTotalResponse
{
    public required LossType Kind { get; init; }
    public required long CostCents { get; init; }
}

/// <summary>RF-REP-08.</summary>
public class LossReportResponse
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required IReadOnlyList<MerchandiseLossResponse> Merchandise { get; init; } = [];
    public required IReadOnlyList<LossTypeTotalResponse> ByKind { get; init; } = [];
    public required long MerchandiseCents { get; init; }
    public required IReadOnlyList<LostShortageResponse> Shortages { get; init; } = [];
    public required long LostShortageCents { get; init; }
    public required long TotalCents { get; init; }

    /// <summary>Counts that found MORE than the stock said, at cost. Informative: it does not offset the losses.</summary>
    public required long CountGainCents { get; init; }
}

// ---------- Product ranking ----------

public class ProductRankingResponse
{
    public required Guid ProductId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required long SoldMilli { get; init; }

    /// <summary>Theoretical sales of the closings (sold × price).</summary>
    public required long SalesCents { get; init; }

    public required long CostCents { get; init; }
    public required long MarginCents { get; init; }
    public int? MarginBps { get; init; }

    /// <summary>Registered shrinkage and count differences (RF-REP-08).</summary>
    public required long LossMilli { get; init; }

    public required long LossCents { get; init; }
}

/// <summary>RF-REP-04. Sorted by sales; the client re-sorts.</summary>
public class ProductRankingReportResponse
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required IReadOnlyList<ProductRankingResponse> Items { get; init; } = [];
}

// ---------- Costs and prices ----------

public class PurchasePointResponse
{
    public required Guid PurchaseId { get; init; }
    public required int PurchaseNumber { get; init; }
    public required DateOnly Date { get; init; }
    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required long RateCupPerUsdCents { get; init; }
    public required long StockQuantityMilli { get; init; }

    /// <summary>Landed (goods + share of the charges), per stock unit.</summary>
    public required long UnitCostUsdMicros { get; init; }

    public required long UnitCostCupMicros { get; init; }
}

/// <summary>RF-REP-03.</summary>
public class SupplierCostResponse
{
    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public required int PurchaseCount { get; init; }
    public required DateOnly LastDate { get; init; }
    public required long LastUnitCostUsdMicros { get; init; }
    public required long LastUnitCostCupMicros { get; init; }
    public required long MinUnitCostUsdMicros { get; init; }
    public required long MaxUnitCostUsdMicros { get; init; }

    /// <summary>Weighted by quantity.</summary>
    public required long AverageUnitCostUsdMicros { get; init; }

    public required long AverageUnitCostCupMicros { get; init; }
    public required long TotalQuantityMilli { get; init; }
}

/// <summary>RF-REP-02/03 for one product.</summary>
public class ProductCostHistoryResponse
{
    public required Guid ProductId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required UnitCode StockUnit { get; init; }
    public long? SalePriceCents { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>Oldest first.</summary>
    public required IReadOnlyList<PurchasePointResponse> Purchases { get; init; } = [];

    /// <summary>Oldest first.</summary>
    public required IReadOnlyList<PriceChangeResponse> PriceChanges { get; init; } = [];

    /// <summary>Cheapest on average first. Never filtered by supplier.</summary>
    public required IReadOnlyList<SupplierCostResponse> Suppliers { get; init; } = [];
}

public class ProductCostChangeResponse
{
    public required Guid ProductId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required int PurchaseCount { get; init; }
    public required DateOnly FirstDate { get; init; }
    public required long FirstUnitCostUsdMicros { get; init; }
    public required long FirstUnitCostCupMicros { get; init; }
    public required DateOnly LastDate { get; init; }
    public required long LastUnitCostUsdMicros { get; init; }
    public required long LastUnitCostCupMicros { get; init; }

    /// <summary>Last vs first. USD = what the supplier charges; CUP adds the exchange rate.</summary>
    public int? UsdChangeBps { get; init; }

    public int? CupChangeBps { get; init; }
    public long? SalePriceCents { get; init; }
}

/// <summary>RF-REP-02 for a category or a supplier. Biggest CUP rise first.</summary>
public class CostChangesReportResponse
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required IReadOnlyList<ProductCostChangeResponse> Items { get; init; } = [];
}
