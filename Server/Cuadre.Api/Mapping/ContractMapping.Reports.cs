using Cuadre.Application.Models;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>Slice 8: the dashboard and the reports (read-only).</summary>
public static partial class ContractMapping
{
    // ---------- Dashboard ----------

    public static DashboardResponse MapToResponse(this Dashboard dashboard)
    {
        return new DashboardResponse
        {
            Today = dashboard.Today,
            LastClosing = dashboard.LastClosing?.MapToResponse(),
            Month = dashboard.Month.MapToResponse(),
            PreviousMonth = dashboard.PreviousMonth.MapToResponse(),
            Days = dashboard.Days.Select(day => day.MapToResponse()).ToList(),
            LowStockCount = dashboard.LowStockCount,
            LowStock = dashboard.LowStock.MapToResponse().Items.ToList(),
            BufferExceededCount = dashboard.BufferExceededCount,
            BufferExceeded = dashboard.BufferExceeded.Select(product => new BufferExceededProductResponse
            {
                ProductId = product.ProductId,
                Code = product.Code,
                Name = product.Name,
                SalePriceCents = product.SalePriceCents,
                SuggestedPriceCents = product.SuggestedPriceCents,
                AverageRateCents = product.AverageRateCents,
                BufferCents = product.EffectiveBufferCents,
                TodayRateCents = product.TodayRateCents
            }).ToList()
        };
    }

    private static DailyFiguresResponse MapToResponse(this DailyFigures day) => new()
    {
        ClosingId = day.ClosingId,
        Date = day.Date,
        CollectedCents = day.CollectedCents,
        CostOfSalesCents = day.CostOfSalesCents,
        RealIncomeCents = day.RealIncomeCents,
        TheoreticalSalesCents = day.TheoreticalSalesCents,
        DifferenceCents = day.AdjustedDifferenceCents,
        StaffPayCents = day.StaffPayCents
    };

    private static PeriodSummaryResponse MapToResponse(this PeriodSummary period) => new()
    {
        From = period.From,
        To = period.To,
        ClosedDays = period.ClosedDays,
        CollectedCents = period.CollectedCents,
        CostOfSalesCents = period.CostOfSalesCents,
        RealIncomeCents = period.RealIncomeCents,
        TheoreticalSalesCents = period.TheoreticalSalesCents,
        StaffPayCents = period.StaffPayCents,
        ShortageCents = period.ShortageCents,
        SurplusCents = period.SurplusCents,
        Expenses = period.Expenses.Select(total => total.MapToResponse()).ToList()
    };

    // ---------- Monthly report ----------

    public static MonthlyReportResponse MapToResponse(this MonthlyReport report)
    {
        return new MonthlyReportResponse
        {
            Year = report.Year,
            Month = report.Month,
            Evaluation = report.Evaluation.MapToResponse(),
            ExpensesByCategory = report.ExpensesByCategory.Select(total => new ExpenseCategoryTotalResponse
            {
                CategoryId = total.CategoryId,
                CategoryName = total.CategoryName,
                Currency = total.Currency.ToContract(),
                Count = total.Count,
                AmountCents = total.AmountCents
            }).ToList(),
            Discrepancies = new DiscrepancySummaryResponse
            {
                ShortageCount = report.Discrepancies.ShortageCount,
                ShortageCents = report.Discrepancies.ShortageCents,
                SurplusCount = report.Discrepancies.SurplusCount,
                SurplusCents = report.Discrepancies.SurplusCents,
                RecoveredCents = report.Discrepancies.RecoveredCents,
                LostCents = report.Discrepancies.LostCents,
                PendingCents = report.Discrepancies.PendingCents
            },
            MerchandiseLossCents = report.MerchandiseLossCents,
            Days = report.Days.Select(day => day.MapToResponse()).ToList(),
            Trend = report.Trend.Select(month => new MonthTotalsResponse
            {
                Year = month.Year,
                Month = month.Month,
                ClosedDays = month.ClosedDays,
                CollectedCents = month.CollectedCents,
                CostOfSalesCents = month.CostOfSalesCents,
                RealIncomeCents = month.RealIncomeCents,
                StaffPayCents = month.StaffPayCents,
                ExpensesCupCents = month.ExpensesCupCents,
                ExpensesUsdCents = month.ExpensesUsdCents
            }).ToList()
        };
    }

    // ---------- Losses ----------

    public static LossReportResponse MapToResponse(this LossReport report)
    {
        return new LossReportResponse
        {
            From = report.From,
            To = report.To,
            Merchandise = report.Merchandise.Select(loss => new MerchandiseLossResponse
            {
                OperationId = loss.OperationId,
                OperationNumber = loss.OperationNumber,
                Date = loss.Date,
                Kind = loss.Kind.ToContract(),
                ProductId = loss.ProductId,
                ProductCode = loss.ProductCode,
                ProductName = loss.ProductName,
                StockUnit = loss.StockUnit.ToContract(),
                QuantityMilli = loss.QuantityMilli,
                UnitCostCupMicros = loss.UnitCostCupMicros,
                CostCents = loss.CostCents
            }).ToList(),
            ByKind = report.Merchandise
                .GroupBy(loss => loss.Kind)
                .OrderBy(group => group.Key)
                .Select(group => new LossTypeTotalResponse
                {
                    Kind = group.Key.ToContract(),
                    CostCents = group.Sum(loss => loss.CostCents)
                }).ToList(),
            MerchandiseCents = report.MerchandiseCents,
            Shortages = report.Shortages.Select(shortage => new LostShortageResponse
            {
                DiscrepancyId = shortage.DiscrepancyId,
                Date = shortage.Date,
                AmountCents = shortage.AmountCents,
                RecoveredCents = shortage.RecoveredCents,
                LostCents = shortage.LostCents,
                EmployeeName = shortage.EmployeeName,
                Note = shortage.Note
            }).ToList(),
            LostShortageCents = report.LostShortageCents,
            TotalCents = report.TotalCents,
            CountGainCents = report.CountGainCents
        };
    }

    // ---------- Product ranking ----------

    public static ProductRankingReportResponse MapToResponse(this ProductRankingReport report)
    {
        return new ProductRankingReportResponse
        {
            From = report.From,
            To = report.To,
            Items = report.Items.Select(item => new ProductRankingResponse
            {
                ProductId = item.ProductId,
                Code = item.Code,
                Name = item.Name,
                StockUnit = item.StockUnit.ToContract(),
                SoldMilli = item.SoldMilli,
                SalesCents = item.SalesCents,
                CostCents = item.CostCents,
                MarginCents = item.MarginCents,
                MarginBps = item.MarginBps,
                LossMilli = item.LossMilli,
                LossCents = item.LossCents
            }).ToList()
        };
    }

    // ---------- Costs and prices ----------

    public static GetProductCostHistoryOptions MapToOptions(this GetProductCostHistoryRequest request) => new()
    {
        From = request.From,
        To = request.To,
        SupplierId = request.SupplierId
    };

    public static ProductCostHistoryResponse MapToResponse(this ProductCostHistory history)
    {
        return new ProductCostHistoryResponse
        {
            ProductId = history.Product.Id,
            Code = history.Product.Code,
            Name = history.Product.Name,
            StockUnit = history.Product.StockUnit.ToContract(),
            SalePriceCents = history.Product.SalePriceCents,
            From = history.From,
            To = history.To,
            Purchases = history.Purchases.Select(point => new PurchasePointResponse
            {
                PurchaseId = point.PurchaseId,
                PurchaseNumber = point.PurchaseNumber,
                Date = point.Date,
                SupplierId = point.SupplierId,
                SupplierName = point.SupplierName,
                Currency = point.Currency.ToContract(),
                RateCupPerUsdCents = point.RateCupPerUsdCents,
                StockQuantityMilli = point.StockQuantityMilli,
                UnitCostUsdMicros = point.LandedUnitCostUsdMicros,
                UnitCostCupMicros = point.LandedUnitCostCupMicros
            }).ToList(),
            PriceChanges = history.PriceChanges.MapToResponse().Items.ToList(),
            Suppliers = history.Suppliers.Select(supplier => new SupplierCostResponse
            {
                SupplierId = supplier.SupplierId,
                SupplierName = supplier.SupplierName,
                PurchaseCount = supplier.PurchaseCount,
                LastDate = supplier.LastDate,
                LastUnitCostUsdMicros = supplier.LastUnitCostUsdMicros,
                LastUnitCostCupMicros = supplier.LastUnitCostCupMicros,
                MinUnitCostUsdMicros = supplier.MinUnitCostUsdMicros,
                MaxUnitCostUsdMicros = supplier.MaxUnitCostUsdMicros,
                AverageUnitCostUsdMicros = supplier.AverageUnitCostUsdMicros,
                AverageUnitCostCupMicros = supplier.AverageUnitCostCupMicros,
                TotalQuantityMilli = supplier.TotalQuantityMilli
            }).ToList()
        };
    }

    public static CostChangesReportResponse MapToResponse(this CostChangesReport report)
    {
        return new CostChangesReportResponse
        {
            From = report.From,
            To = report.To,
            Items = report.Items.Select(item => new ProductCostChangeResponse
            {
                ProductId = item.ProductId,
                Code = item.Code,
                Name = item.Name,
                StockUnit = item.StockUnit.ToContract(),
                PurchaseCount = item.PurchaseCount,
                FirstDate = item.FirstDate,
                FirstUnitCostUsdMicros = item.FirstUnitCostUsdMicros,
                FirstUnitCostCupMicros = item.FirstUnitCostCupMicros,
                LastDate = item.LastDate,
                LastUnitCostUsdMicros = item.LastUnitCostUsdMicros,
                LastUnitCostCupMicros = item.LastUnitCostCupMicros,
                UsdChangeBps = item.UsdChangeBps,
                CupChangeBps = item.CupChangeBps,
                SalePriceCents = item.SalePriceCents
            }).ToList()
        };
    }

    // ---------- Enums ----------

    private static LossType ToContract(this LossKind kind) => kind switch
    {
        LossKind.Shrinkage => LossType.Shrinkage,
        LossKind.Expired => LossType.Expired,
        LossKind.Damaged => LossType.Damaged,
        LossKind.CountDifference => LossType.CountDifference,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
