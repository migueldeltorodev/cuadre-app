using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.CashBoxes;
using Cuadre.Api.Tests.Integration.Catalog;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Api.Tests.Integration.Inventory;
using Cuadre.Api.Tests.Integration.Purchases;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;
using static Cuadre.Api.Tests.Integration.Closings.ClosingTestHelpers;
using static Cuadre.Api.Tests.Integration.Inventory.InventoryTestHelpers;
using static Cuadre.Api.Tests.Integration.Purchases.PurchaseTestHelpers;

namespace Cuadre.Api.Tests.Integration.Reports;

// Slice 8 (RF-REP-01..05, 07, 08). Today is Thursday 2026-11-12.
// The rice of the closing tests: 1 sack = 100 lb at USD 40 and 325 → 130 CUP the lb; sold at 250 CUP the lb.
// Selling the 100 lb: A1 25,000 CUP, A2 13,000, A3 12,000, staff 1,250 (1,000 + 1%).

[Collection(nameof(ApiCollection))]
public class ReportEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const long PricePerLb = 25_000;
    private const long CostPerLbMicros = 130_000_000;   // 130 CUP
    private const long Shrinkage = 50_000;               // 2% of 25,000 CUP of sales

    private DateOnly Yesterday => Clock.Today.AddDays(-1);

    private async Task<(ProductResponse Rice, SupplierResponse Supplier)> RiceBoughtAsync(DateOnly date,
        long usdCentsPerSack = 4_000, long sacks = 1)
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct, PricePerLb);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, date, Ct, Sacks(rice.Id, sacks, usdCentsPerSack)), Ct);
        return (rice, supplier);
    }

    /// <summary>Buys a sack, moves it to the counter and sells all of it on <paramref name="date"/>.</summary>
    private async Task<DailyClosingResponse> SellEverythingAsync(DateOnly date, long missingCents = 0)
    {
        var (rice, _) = await RiceBoughtAsync(date);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);
        await Client.TransferAsync(date, storage.Id, counter.Id, Ct, SacksOf(rice.Id, 1));
        var draft = await Client.GetDraftAsync(date, Ct);
        return await Client.CloseAsync(new CreateDailyClosingRequest
        {
            Date = date,
            // Beyond the threshold the closing needs a note (RF-CIE-12).
            Notes = missingCents > 0 ? "Faltó plata" : null,
            Counts = [Counted(rice.Id, counter.Id, 0)],
            Collections = [draft.Collected("Efectivo", 2_500_000 - (missingCents > 0 ? Shrinkage + missingCents : 0))]
        }, Ct);
    }

    private async Task CreateExpenseAsync(long amountCents, string category = "Transporte")
    {
        var categoryId = (await Client.GetFromJsonAsync<ExpenseCategoriesResponse>(
            ApiEndpoints.ExpenseCategories.GetAll, Ct))!.Items.Single(x => x.Name == category).Id;
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Expenses.Create, new CreateExpenseRequest
        {
            Date = Clock.Today,
            CategoryId = categoryId,
            Description = "Flete",
            AmountCents = amountCents,
            CashBoxId = (await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct)).Id
        }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<T> GetOkAsync<T>(string route)
    {
        var response = await Client.GetAsync(route, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    // ---------- Dashboard (RF-REP-01) ----------

    [Fact]
    public async Task Dashboard_ShouldBeEmpty_BeforeTheFirstClosing()
    {
        var dashboard = await GetOkAsync<DashboardResponse>(ApiEndpoints.Dashboard.Get);

        dashboard.Today.Should().Be(Clock.Today);
        dashboard.LastClosing.Should().BeNull();
        dashboard.Month.From.Should().Be(new DateOnly(2026, 11, 1));
        dashboard.Month.To.Should().Be(Clock.Today);
        dashboard.Month.ClosedDays.Should().Be(0);
        dashboard.Month.Expenses.Should().BeEmpty();
        dashboard.Days.Should().BeEmpty();
        dashboard.BufferExceededCount.Should().Be(0);
    }

    [Fact]
    public async Task Dashboard_ShouldCompareWithTheSameDaysOfThePreviousMonth()
    {
        var dashboard = await GetOkAsync<DashboardResponse>(ApiEndpoints.Dashboard.Get);

        dashboard.PreviousMonth.From.Should().Be(new DateOnly(2026, 10, 1));
        dashboard.PreviousMonth.To.Should().Be(new DateOnly(2026, 10, 12));
    }

    [Fact]
    public async Task Dashboard_ShouldSumTheMonth_AndShowTheLastClosingAndTheExpenses()
    {
        var closing = await SellEverythingAsync(Yesterday);
        await CreateExpenseAsync(50_000);

        var dashboard = await GetOkAsync<DashboardResponse>(ApiEndpoints.Dashboard.Get);

        dashboard.LastClosing!.ClosingId.Should().Be(closing.Id);
        dashboard.LastClosing.Date.Should().Be(Yesterday);
        dashboard.LastClosing.CollectedCents.Should().Be(2_500_000);
        dashboard.LastClosing.CostOfSalesCents.Should().Be(1_300_000);
        dashboard.LastClosing.RealIncomeCents.Should().Be(1_200_000);

        dashboard.Month.ClosedDays.Should().Be(1);
        dashboard.Month.CollectedCents.Should().Be(2_500_000);
        dashboard.Month.RealIncomeCents.Should().Be(1_200_000);
        dashboard.Month.StaffPayCents.Should().Be(125_000);
        dashboard.Month.Expenses.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Currency = CurrencyCode.CUP, AmountCents = 50_000L });
        dashboard.PreviousMonth.ClosedDays.Should().Be(0);
        dashboard.Days.Should().ContainSingle().Which.Date.Should().Be(Yesterday);
    }

    [Fact]
    public async Task Dashboard_ShouldListTheLowStock()
    {
        // Everything sold: 0 lb left, the minimum is 20 lb.
        await SellEverythingAsync(Yesterday);

        var dashboard = await GetOkAsync<DashboardResponse>(ApiEndpoints.Dashboard.Get);

        dashboard.LowStockCount.Should().BeGreaterThanOrEqualTo(1);
        dashboard.LowStock.Should().NotBeEmpty().And.HaveCountLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task Dashboard_ShouldFlagTheBufferExceeded_WhenTodaysRateGoesBeyondThePurchaseRate()
    {
        var (rice, _) = await RiceBoughtAsync(Clock.Today);
        var before = await GetOkAsync<DashboardResponse>(ApiEndpoints.Dashboard.Get);

        // Bought at 325, the default colchón is 0: 330 is beyond it.
        var rate = await Client.PutAsJsonAsync(Route(ApiEndpoints.ExchangeRates.Upsert, Clock.Today),
            new UpsertExchangeRateRequest { CupPerUsdCents = 33_000 }, Ct);
        rate.EnsureSuccessStatusCode();
        var after = await GetOkAsync<DashboardResponse>(ApiEndpoints.Dashboard.Get);

        before.BufferExceededCount.Should().Be(0);
        after.BufferExceededCount.Should().Be(1);
        var flagged = after.BufferExceeded.Should().ContainSingle().Subject;
        flagged.ProductId.Should().Be(rice.Id);
        flagged.LastRateCents.Should().Be(Rate325);
        flagged.TodayRateCents.Should().Be(33_000);
    }

    // ---------- Monthly report (RF-REP-05, RF-REP-07) ----------

    [Fact]
    public async Task Monthly_ShouldGatherTheMonth_WithItsExpensesDifferencesAndTrend()
    {
        // Yesterday: 1,000 CUP missing beyond the shrinkage. Today: a 500 CUP expense.
        await SellEverythingAsync(Yesterday, missingCents: 100_000);
        await CreateExpenseAsync(50_000);

        var report = await GetOkAsync<MonthlyReportResponse>(ApiEndpoints.Reports.Monthly);

        report.Year.Should().Be(2026);
        report.Month.Should().Be(11);
        report.Evaluation.From.Should().Be(new DateOnly(2026, 11, 1));
        report.Evaluation.To.Should().Be(Clock.Today);
        report.Evaluation.ClosedDays.Should().Be(1);
        report.Evaluation.CollectedCents.Should().Be(2_500_000 - Shrinkage - 100_000);
        report.Evaluation.Expenses.Should().ContainSingle().Which.AmountCents.Should().Be(50_000);

        var transport = report.ExpensesByCategory.Should().ContainSingle().Subject;
        transport.CategoryName.Should().Be("Transporte");
        transport.Count.Should().Be(1);
        transport.AmountCents.Should().Be(50_000);

        report.Discrepancies.ShortageCount.Should().Be(1);
        report.Discrepancies.ShortageCents.Should().Be(100_000);
        report.Discrepancies.PendingCents.Should().Be(100_000);
        report.Discrepancies.LostCents.Should().Be(0);

        report.Days.Should().ContainSingle().Which.DifferenceCents.Should().Be(-100_000);

        report.Trend.Should().HaveCount(12);
        (report.Trend[0].Year, report.Trend[0].Month).Should().Be((2025, 12));
        report.Trend[^1].Month.Should().Be(11);
        report.Trend[^1].ClosedDays.Should().Be(1);
        report.Trend[^1].ExpensesCupCents.Should().Be(50_000);
        report.Trend[^2].ClosedDays.Should().Be(0);
    }

    [Fact]
    public async Task Monthly_ShouldRunAPastMonthToItsLastDay()
    {
        var report = await GetOkAsync<MonthlyReportResponse>($"{ApiEndpoints.Reports.Monthly}?year=2026&month=10");

        report.Evaluation.From.Should().Be(new DateOnly(2026, 10, 1));
        report.Evaluation.To.Should().Be(new DateOnly(2026, 10, 31));
        report.Trend[^1].Month.Should().Be(10);
    }

    [Theory]
    [InlineData("?year=2026&month=12")]   // has not started yet
    [InlineData("?year=2026&month=13")]
    [InlineData("?year=1999&month=1")]
    public async Task Monthly_ShouldReturn400_ForAMonthThatIsNotValid(string query)
    {
        var response = await Client.GetAsync(ApiEndpoints.Reports.Monthly + query, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- Losses (RF-REP-08) ----------

    [Fact]
    public async Task Losses_ShouldValueTheShrinkageAndTheCountDifferencesAtCost()
    {
        // 100 lb in the storage. Out: 2 lb of shrinkage, 1 lb of adjustment (not a loss). Then 90 counted of 97.
        var (rice, _) = await RiceBoughtAsync(Clock.Today);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);
        await Client.ExitAsync(Clock.Today, storage.Id, ExitReasonCode.Shrinkage, Ct, Pounds(rice.Id, 2));
        await Client.ExitAsync(Clock.Today, storage.Id, ExitReasonCode.Adjustment, Ct, Pounds(rice.Id, 1));
        await Client.CountAsync(Clock.Today, storage.Id, Ct, Pounds(rice.Id, 90));

        var report = await GetOkAsync<LossReportResponse>(ApiEndpoints.Reports.Losses);

        report.From.Should().Be(new DateOnly(2026, 11, 1));
        report.To.Should().Be(Clock.Today);
        report.Merchandise.Should().HaveCount(2);
        var shrinkage = report.Merchandise.Single(x => x.Kind == LossType.Shrinkage);
        shrinkage.QuantityMilli.Should().Be(2_000);
        shrinkage.UnitCostCupMicros.Should().Be(CostPerLbMicros);
        shrinkage.CostCents.Should().Be(26_000);
        var count = report.Merchandise.Single(x => x.Kind == LossType.CountDifference);
        count.QuantityMilli.Should().Be(7_000);
        count.CostCents.Should().Be(91_000);
        report.ByKind.Should().HaveCount(2);
        report.MerchandiseCents.Should().Be(117_000);
        report.Shortages.Should().BeEmpty();
        report.TotalCents.Should().Be(117_000);
        report.CountGainCents.Should().Be(0);
    }

    [Fact]
    public async Task Losses_ShouldIncludeTheShortagesWrittenOff_MinusWhatCameBack()
    {
        var closing = await SellEverythingAsync(Yesterday, missingCents: 100_000);
        var cash = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        var discrepancyId = closing.Discrepancy!.Id;
        (await Client.PostAsJsonAsync(Route(ApiEndpoints.CashDiscrepancies.Recover, discrepancyId),
            new RecoverShortageRequest { CashBoxId = cash.Id, AmountCents = 30_000 }, Ct)).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync(Route(ApiEndpoints.CashDiscrepancies.WriteOff, discrepancyId),
            new WriteOffShortageRequest { Note = "No apareció" }, Ct)).EnsureSuccessStatusCode();

        var report = await GetOkAsync<LossReportResponse>(ApiEndpoints.Reports.Losses);
        var monthly = await GetOkAsync<MonthlyReportResponse>(ApiEndpoints.Reports.Monthly);

        var shortage = report.Shortages.Should().ContainSingle().Subject;
        shortage.Date.Should().Be(Yesterday);
        shortage.AmountCents.Should().Be(100_000);
        shortage.RecoveredCents.Should().Be(30_000);
        shortage.LostCents.Should().Be(70_000);
        shortage.Note.Should().Be("No apareció");
        report.LostShortageCents.Should().Be(70_000);
        report.TotalCents.Should().Be(70_000);

        monthly.Discrepancies.RecoveredCents.Should().Be(30_000);
        monthly.Discrepancies.LostCents.Should().Be(70_000);
        monthly.Discrepancies.PendingCents.Should().Be(0);
    }

    [Fact]
    public async Task Losses_ShouldReturn400_WhenToIsBeforeFrom()
    {
        var response = await Client.GetAsync($"{ApiEndpoints.Reports.Losses}?from=2026-11-10&to=2026-11-01", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- Product ranking (RF-REP-04) ----------

    [Fact]
    public async Task Ranking_ShouldShowSalesMarginAndLossesPerProduct()
    {
        await SellEverythingAsync(Yesterday);

        // Another product, only lost: 5 lb of a sack bought today.
        var (beans, _) = await RiceBoughtAsync(Clock.Today);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);
        await Client.ExitAsync(Clock.Today, storage.Id, ExitReasonCode.Damaged, Ct, Pounds(beans.Id, 5));

        var report = await GetOkAsync<ProductRankingReportResponse>(ApiEndpoints.Reports.ProductRanking);

        report.Items.Should().HaveCount(2);
        var sold = report.Items[0];
        sold.SoldMilli.Should().Be(100_000);
        sold.SalesCents.Should().Be(2_500_000);
        sold.CostCents.Should().Be(1_300_000);
        sold.MarginCents.Should().Be(1_200_000);
        sold.MarginBps.Should().Be(4_800);
        sold.LossCents.Should().Be(0);

        var lost = report.Items[1];
        lost.ProductId.Should().Be(beans.Id);
        lost.SalesCents.Should().Be(0);
        lost.MarginBps.Should().BeNull();
        lost.LossMilli.Should().Be(5_000);
        lost.LossCents.Should().Be(65_000);
    }

    [Fact]
    public async Task Ranking_ShouldFilterByCategory()
    {
        await SellEverythingAsync(Yesterday);
        var otherCategory = await Client.CreateCategoryAsync("Bebidas", Ct, codePrefix: "BEB");

        var report = await GetOkAsync<ProductRankingReportResponse>(
            $"{ApiEndpoints.Reports.ProductRanking}?categoryId={otherCategory.Id}");

        report.Items.Should().BeEmpty();
    }

    // ---------- Costs (RF-REP-02, RF-REP-03) ----------

    /// <summary>Supplier A: USD 40 ten days ago and USD 44 today. Supplier B: USD 38 five days ago.</summary>
    private async Task<(ProductResponse Rice, SupplierResponse A, SupplierResponse B)> BoughtFromTwoSuppliersAsync()
    {
        var (rice, a) = await RiceBoughtAsync(Clock.Today.AddDays(-10), 4_000);
        var b = await Client.CreateSupplierAsync(Ct);
        await Client.CreatePurchaseAsync(await Client.UsdPurchaseRequestAsync(b.Id, Clock.Today.AddDays(-5), Ct,
            Sacks(rice.Id, 1, 3_800)), Ct);
        await Client.CreatePurchaseAsync(await Client.UsdPurchaseRequestAsync(a.Id, Clock.Today, Ct,
            Sacks(rice.Id, 1, 4_400)), Ct);
        return (rice, a, b);
    }

    [Fact]
    public async Task ProductCosts_ShouldListEveryPurchase_AndCompareTheSuppliers()
    {
        var (rice, a, b) = await BoughtFromTwoSuppliersAsync();

        var history = await GetOkAsync<ProductCostHistoryResponse>(Route(ApiEndpoints.Reports.ProductCosts, rice.Id));

        history.ProductId.Should().Be(rice.Id);
        history.StockUnit.Should().Be(UnitCode.Pound);
        history.Purchases.Select(x => x.UnitCostUsdMicros).Should().Equal(400_000, 380_000, 440_000);
        history.Purchases[0].UnitCostCupMicros.Should().Be(CostPerLbMicros);
        history.PriceChanges.Should().NotBeEmpty();

        history.Suppliers.Should().HaveCount(2);
        var cheapest = history.Suppliers[0];
        cheapest.SupplierId.Should().Be(b.Id);
        cheapest.AverageUnitCostUsdMicros.Should().Be(380_000);
        var usual = history.Suppliers[1];
        usual.SupplierId.Should().Be(a.Id);
        usual.PurchaseCount.Should().Be(2);
        usual.LastDate.Should().Be(Clock.Today);
        usual.LastUnitCostUsdMicros.Should().Be(440_000);
        usual.MinUnitCostUsdMicros.Should().Be(400_000);
        usual.MaxUnitCostUsdMicros.Should().Be(440_000);
        usual.AverageUnitCostUsdMicros.Should().Be(420_000);
        usual.TotalQuantityMilli.Should().Be(200_000);
    }

    [Fact]
    public async Task ProductCosts_ShouldFilterTheChartBySupplier_ButKeepTheComparison()
    {
        var (rice, _, b) = await BoughtFromTwoSuppliersAsync();

        var history = await GetOkAsync<ProductCostHistoryResponse>(
            $"{Route(ApiEndpoints.Reports.ProductCosts, rice.Id)}?supplierId={b.Id}");

        history.Purchases.Should().ContainSingle().Which.SupplierId.Should().Be(b.Id);
        history.Suppliers.Should().HaveCount(2);
    }

    [Fact]
    public async Task ProductCosts_ShouldReturn404_WhenTheProductDoesNotExist()
    {
        var response = await Client.GetAsync(Route(ApiEndpoints.Reports.ProductCosts, Guid.NewGuid()), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CostChanges_ShouldCompareTheFirstAndTheLastCostOfThePeriod()
    {
        var (rice, _, b) = await BoughtFromTwoSuppliersAsync();

        var all = await GetOkAsync<CostChangesReportResponse>(ApiEndpoints.Reports.CostChanges);
        var onlyB = await GetOkAsync<CostChangesReportResponse>($"{ApiEndpoints.Reports.CostChanges}?supplierId={b.Id}");

        all.From.Should().Be(new DateOnly(2025, 11, 13));
        all.To.Should().Be(Clock.Today);
        var change = all.Items.Should().ContainSingle().Subject;
        change.ProductId.Should().Be(rice.Id);
        change.PurchaseCount.Should().Be(3);
        change.FirstDate.Should().Be(Clock.Today.AddDays(-10));
        change.FirstUnitCostUsdMicros.Should().Be(400_000);
        change.LastUnitCostUsdMicros.Should().Be(440_000);
        change.UsdChangeBps.Should().Be(1_000);
        change.CupChangeBps.Should().Be(1_000);   // same rate: the CUP cost moves like the USD one
        change.SalePriceCents.Should().Be(PricePerLb);

        onlyB.Items.Should().ContainSingle().Which.UsdChangeBps.Should().Be(0);
    }
}
