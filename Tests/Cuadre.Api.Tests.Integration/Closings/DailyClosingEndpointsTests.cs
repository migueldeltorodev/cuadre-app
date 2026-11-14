using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.CashBoxes;
using Cuadre.Api.Tests.Integration.Envelopes;
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

namespace Cuadre.Api.Tests.Integration.Closings;

// The rice: 100 lb sacks sold by the lb at 250 CUP, 2% of shrinkage accepted.
// Bought at 40 USD the sack at 325 → 0.40 USD/lb = 130 CUP/lb. One sack (100 lb) is opened at the counter.
// Seeded plan: Ganancia 5%, Dueño 50%, Impuestos 15%, Operación 30%. Seeded pay: 1,000 CUP + 1% of A1.

[Collection(nameof(ApiCollection))]
public class DailyClosingEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const long PricePerLb = 25_000;

    private async Task<(ProductResponse Rice, LocationResponse Counter)> RiceAtTheCounterAsync(DateOnly date)
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct, PricePerLb);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, date, Ct, Sacks(rice.Id, 1, 4_000)), Ct);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);
        await Client.TransferAsync(date, storage.Id, counter.Id, Ct, SacksOf(rice.Id, 1));
        return (rice, counter);
    }

    private static CreateDailyClosingRequest Request(DateOnly date, ClosingCountRequest count,
        params ClosingCollectionRequest[] collections) => new()
    {
        Date = date,
        Counts = [count],
        Collections = collections
    };

    // ---------- Draft ----------

    [Fact]
    public async Task Draft_ShouldListTheCounterAndTheCupPaymentMethods()
    {
        var (rice, counter) = await RiceAtTheCounterAsync(Clock.Today);

        var draft = await Client.GetDraftAsync(Clock.Today, Ct);

        draft.CanClose.Should().BeTrue();
        draft.Problem.Should().BeNull();
        draft.LastClosedDate.Should().BeNull();
        draft.DebtBeforeCents.Should().Be(0);
        draft.PayScheme.DailyFixedCents.Should().Be(100_000);
        draft.PayScheme.PercentOfSalesBps.Should().Be(100);

        var line = draft.Lines.Should().ContainSingle().Subject;
        line.ProductId.Should().Be(rice.Id);
        line.LocationId.Should().Be(counter.Id);
        line.OpeningMilli.Should().Be(0);
        line.InMilli.Should().Be(100_000);
        line.ExpectedMilli.Should().Be(100_000);
        line.UnitPriceCents.Should().Be(PricePerLb);
        line.AllowsDecimals.Should().BeTrue();

        // Efectivo → cash; Transfermóvil and Enzona → bank. Nothing in USD.
        draft.PaymentMethods.Select(x => x.Name).Should().BeEquivalentTo("Efectivo", "Transfermóvil", "Enzona");
        draft.PaymentMethods.Single(x => x.Name == "Efectivo").IsTransfer.Should().BeFalse();
        draft.PaymentMethods.Single(x => x.Name == "Transfermóvil").IsTransfer.Should().BeTrue();
    }

    [Fact]
    public async Task Draft_ShouldNotLetCloseAFutureDay()
    {
        var draft = await Client.GetDraftAsync(Clock.Today.AddDays(1), Ct);

        draft.CanClose.Should().BeFalse();
        draft.Problem.Should().NotBeNull();
    }

    // ---------- Close ----------

    [Fact]
    public async Task Create_ShouldComputeTheDayAndSplitIt()
    {
        // Arrange: everything sold. Sales 100 lb × 250 = 25,000; A2 = 100 × 130 = 13,000.
        var (rice, counter) = await RiceAtTheCounterAsync(Clock.Today);
        var draft = await Client.GetDraftAsync(Clock.Today, Ct);

        // Act: 20,000 cash + 5,000 Transfermóvil = A1 25,000 → no difference.
        var closing = await Client.CloseAsync(Request(Clock.Today, Counted(rice.Id, counter.Id, 0),
            draft.Collected("Efectivo", 2_000_000), draft.Collected("Transfermóvil", 500_000)), Ct);

        // Assert: A3 = 12,000; pay = 1,000 + 250 = 1,250; transfers below A2 → no debt, 12,000 to the others.
        closing.Status.Should().Be(DailyClosingState.Confirmed);
        closing.TheoreticalSalesCents.Should().Be(2_500_000);
        closing.ShrinkageToleranceCents.Should().Be(50_000);
        closing.CollectedCents.Should().Be(2_500_000);
        closing.CostOfSalesCents.Should().Be(1_300_000);
        closing.RealIncomeCents.Should().Be(1_200_000);
        closing.CashDifferenceCents.Should().Be(0);
        closing.AdjustedDifferenceCents.Should().Be(0);
        closing.Discrepancy.Should().BeNull();
        closing.StaffPayCents.Should().Be(125_000);
        closing.DebtCreatedCents.Should().Be(0);
        closing.DebtAfterCents.Should().Be(0);

        var line = closing.Lines.Should().ContainSingle().Subject;
        line.SoldMilli.Should().Be(100_000);
        line.SalesCents.Should().Be(2_500_000);
        line.CostCents.Should().Be(1_300_000);

        // 12,000 by 5/50/15/30 → 600 / 6,000 / 1,800 / 3,600. Reposición: 13,000 = 5,000 bank + 8,000 cash.
        var replenishment = closing.Allocation(EnvelopeType.Replenishment);
        replenishment.AmountCents.Should().Be(1_300_000);
        replenishment.CashCents.Should().Be(800_000);
        replenishment.BankCents.Should().Be(500_000);
        closing.Allocation(EnvelopeType.Profit).AmountCents.Should().Be(60_000);
        closing.Allocation(EnvelopeType.OwnerPay).AmountCents.Should().Be(600_000);
        closing.Allocation(EnvelopeType.Tax).AmountCents.Should().Be(180_000);
        var operations = closing.Allocation(EnvelopeType.Operations);
        operations.AmountCents.Should().Be(360_000);
        operations.CashCents.Should().Be(235_000);   // 3,600 − 1,250 already paid

        // The envelopes, box by box
        var cash = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        var bank = await Client.GetCashBoxByNameAsync("Banco CUP", Ct);
        var overview = await Client.GetOverviewAsync(Ct);
        overview.Balance(EnvelopeType.Replenishment, cash.Id).Should().Be(800_000);
        overview.Balance(EnvelopeType.Replenishment, bank.Id).Should().Be(500_000);
        overview.Balance(EnvelopeType.Profit, cash.Id).Should().Be(60_000);
        overview.Balance(EnvelopeType.OwnerPay, cash.Id).Should().Be(600_000);
        overview.Balance(EnvelopeType.Tax, cash.Id).Should().Be(180_000);
        overview.Balance(EnvelopeType.Operations, cash.Id).Should().Be(235_000);
        overview.BoxTotal(cash.Id).Should().Be(1_875_000);   // 20,000 − 1,250
        overview.InternalDebtCents.Should().Be(0);

        // The counter is empty
        (await Client.GetStockAsync(Ct)).QuantityAt(rice.Id, counter.Id).Should().Be(0);
    }

    [Fact]
    public async Task Create_ShouldCostTheSales_AtTheLastPurchase_SoReposicionCanBuyItAgain()
    {
        // D-54: two sacks, at 40 USD (130 CUP/lb) and then 46 USD (149.50 CUP/lb). The average would be 139.75,
        // but restocking costs 149.50: A2 = 100 lb x 149.50 = 14,950.
        var (rice, supplier) = await Client.CreateRiceAsync(Ct, PricePerLb);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 4_000)), Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 4_600)), Ct);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);
        await Client.TransferAsync(Clock.Today, storage.Id, counter.Id, Ct, SacksOf(rice.Id, 1));
        var draft = await Client.GetDraftAsync(Clock.Today, Ct);

        var closing = await Client.CloseAsync(Request(Clock.Today, Counted(rice.Id, counter.Id, 0),
            draft.Collected("Efectivo", 2_500_000)), Ct);

        closing.CostOfSalesCents.Should().Be(1_495_000);
        closing.RealIncomeCents.Should().Be(1_005_000);
        closing.Lines.Should().ContainSingle().Which.CostCents.Should().Be(1_495_000);
        closing.Allocation(EnvelopeType.Replenishment).AmountCents.Should().Be(1_495_000);
    }

    [Fact]
    public async Task Create_ShouldCreateDebt_ThenRepayIt_WhenTheTransfersGoBeyondA2()
    {
        var yesterday = Clock.Today.AddDays(-1);
        var (rice, counter) = await RiceAtTheCounterAsync(yesterday);

        // Day 1: 40 lb sold → 10,000 of sales, A2 = 5,200, all collected by transfer.
        // A3 = 4,800 stays in the bank, in Reposición: it owes 4,800 to the others.
        var draft1 = await Client.GetDraftAsync(yesterday, Ct);
        var day1 = await Client.CloseAsync(Request(yesterday, Counted(rice.Id, counter.Id, 60),
            draft1.Collected("Transfermóvil", 1_000_000)), Ct);

        day1.DebtCreatedCents.Should().Be(480_000);
        day1.DebtAfterCents.Should().Be(480_000);
        day1.Allocation(EnvelopeType.Profit).AmountCents.Should().Be(0);
        (await Client.GetOverviewAsync(Ct, $"?date={yesterday:yyyy-MM-dd}")).InternalDebtCents.Should().Be(480_000);

        // Day 2: 20 lb sold → 5,000 of sales, A2 = 2,600, all in cash. A3 = 2,400.
        // Reposición's 2,600 of cash pay the debt back: the others get 2,400 + 2,600 = 5,000; debt left 2,200.
        var draft2 = await Client.GetDraftAsync(Clock.Today, Ct);
        draft2.DebtBeforeCents.Should().Be(480_000);
        draft2.Lines.Single().OpeningMilli.Should().Be(60_000);

        var day2 = await Client.CloseAsync(Request(Clock.Today, Counted(rice.Id, counter.Id, 40),
            draft2.Collected("Efectivo", 500_000)), Ct);

        day2.DebtBeforeCents.Should().Be(480_000);
        day2.DebtRepaidCents.Should().Be(260_000);
        day2.DebtAfterCents.Should().Be(220_000);
        day2.Allocation(EnvelopeType.Replenishment).AmountCents.Should().Be(0);
        day2.Allocations.Where(x => x.Role != EnvelopeType.Replenishment).Sum(x => x.AmountCents)
            .Should().Be(500_000);
        (await Client.GetOverviewAsync(Ct)).InternalDebtCents.Should().Be(220_000);
    }

    [Fact]
    public async Task Create_ShouldRecordAShortage_AndRequireANote_WhenBeyondTheThreshold()
    {
        var (rice, counter) = await RiceAtTheCounterAsync(Clock.Today);
        var draft = await Client.GetDraftAsync(Clock.Today, Ct);

        // 25,000 of sales, 500 of shrinkage accepted: 100 CUP more than threshold + shrinkage are missing.
        var missing = draft.ThresholdCents + 50_000 + 10_000;
        var request = Request(Clock.Today, Counted(rice.Id, counter.Id, 0),
            draft.Collected("Efectivo", 2_500_000 - missing));

        var preview = await Client.PostAsJsonAsync(ApiEndpoints.DailyClosings.Preview, request, Ct);
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var previewed = (await preview.Content.ReadFromJsonAsync<DailyClosingResponse>(Ct))!;
        previewed.RequiresNote.Should().BeTrue();
        previewed.AdjustedDifferenceCents.Should().Be(-(draft.ThresholdCents + 10_000));

        var withoutNote = await Client.PostAsJsonAsync(ApiEndpoints.DailyClosings.Create, request, Ct);
        withoutNote.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var closing = await Client.CloseAsync(new CreateDailyClosingRequest
        {
            Date = request.Date,
            Notes = "Se dio mal un vuelto",
            Counts = request.Counts,
            Collections = request.Collections
        }, Ct);

        closing.Discrepancy.Should().NotBeNull();
        var discrepancy = closing.Discrepancy!;
        discrepancy.Kind.Should().Be(CashDiscrepancyType.Shortage);
        discrepancy.Status.Should().Be(CashDiscrepancyState.Pending);
        discrepancy.AmountCents.Should().Be(draft.ThresholdCents + 10_000);
        discrepancy.Note.Should().Be("Se dio mal un vuelto");
    }

    [Fact]
    public async Task Preview_ShouldNotSaveAnything()
    {
        var (rice, counter) = await RiceAtTheCounterAsync(Clock.Today);
        var draft = await Client.GetDraftAsync(Clock.Today, Ct);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.DailyClosings.Preview,
            Request(Clock.Today, Counted(rice.Id, counter.Id, 0), draft.Collected("Efectivo", 2_500_000)), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetDraftAsync(Clock.Today, Ct)).CanClose.Should().BeTrue();
        (await Client.GetStockAsync(Ct)).QuantityAt(rice.Id, counter.Id).Should().Be(100_000);
    }

    [Fact]
    public async Task Create_ShouldReturn422_WhenAProductOfTheCounterIsNotCounted()
    {
        await RiceAtTheCounterAsync(Clock.Today);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.DailyClosings.Create, new CreateDailyClosingRequest
        {
            Date = Clock.Today,
            Counts = [],
            Collections = []
        }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenACountIsNegative()
    {
        var (rice, counter) = await RiceAtTheCounterAsync(Clock.Today);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.DailyClosings.Create,
            Request(Clock.Today, Counted(rice.Id, counter.Id, -1)), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ShouldReturn422_WhenTheDayIsAlreadyClosed()
    {
        var (rice, counter) = await RiceAtTheCounterAsync(Clock.Today);
        var request = Request(Clock.Today, Counted(rice.Id, counter.Id, 100));
        await Client.CloseAsync(request, Ct);

        var again = await Client.PostAsJsonAsync(ApiEndpoints.DailyClosings.Create, request, Ct);

        again.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ---------- Lock (RF-CIE-10) ----------

    [Fact]
    public async Task ClosedDay_ShouldRefuseAnythingDatedOnOrBeforeIt()
    {
        var (rice, counter) = await RiceAtTheCounterAsync(Clock.Today);
        await Client.CloseAsync(Request(Clock.Today, Counted(rice.Id, counter.Id, 100)), Ct);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);

        var transfer = await Client.PostAsJsonAsync(ApiEndpoints.StockOperations.CreateTransfer,
            new CreateTransferRequest
            {
                Date = Clock.Today,
                FromLocationId = counter.Id,
                ToLocationId = storage.Id,
                Lines = [Pounds(rice.Id, 1)]
            }, Ct);
        transfer.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var (_, supplier) = await Client.CreateRiceAsync(Ct);
        var purchase = await Client.PostAsJsonAsync(ApiEndpoints.Purchases.Create,
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 4_000)), Ct);
        purchase.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var scheme = await Client.PostAsJsonAsync(ApiEndpoints.StaffPaySchemes.Create, new CreateStaffPaySchemeRequest
        {
            EffectiveFrom = Clock.Today,
            DailyFixedCents = 120_000,
            PercentOfSalesBps = 100
        }, Ct);
        scheme.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // An earlier day cannot be closed any more
        var draft = await Client.GetDraftAsync(Clock.Today.AddDays(-1), Ct);
        draft.CanClose.Should().BeFalse();
    }

    // ---------- Reopen ----------

    [Fact]
    public async Task Reopen_ShouldUndoTheDay_AndLetItBeClosedAgain()
    {
        var (rice, counter) = await RiceAtTheCounterAsync(Clock.Today);
        var draft = await Client.GetDraftAsync(Clock.Today, Ct);
        var request = Request(Clock.Today, Counted(rice.Id, counter.Id, 0), draft.Collected("Efectivo", 2_500_000));
        var closing = await Client.CloseAsync(request, Ct);

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.DailyClosings.Reopen, closing.Id),
            new ReopenDailyClosingRequest { Reason = "Faltó contar un saco" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var reopened = (await response.Content.ReadFromJsonAsync<DailyClosingResponse>(Ct))!;
        reopened.Status.Should().Be(DailyClosingState.Voided);
        reopened.VoidReason.Should().Be("Faltó contar un saco");

        // Back to how it was before closing
        (await Client.GetStockAsync(Ct)).QuantityAt(rice.Id, counter.Id).Should().Be(100_000);
        var cash = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        (await Client.GetOverviewAsync(Ct)).BoxTotal(cash.Id).Should().Be(0);
        (await Client.GetDraftAsync(Clock.Today, Ct)).CanClose.Should().BeTrue();

        // And it closes again
        var again = await Client.CloseAsync(request, Ct);
        again.Status.Should().Be(DailyClosingState.Confirmed);
    }

    [Fact]
    public async Task Reopen_ShouldReturn422_WhenItIsNotTheLatestClosedDay()
    {
        var yesterday = Clock.Today.AddDays(-1);
        var (rice, counter) = await RiceAtTheCounterAsync(yesterday);
        var day1 = await Client.CloseAsync(Request(yesterday, Counted(rice.Id, counter.Id, 100)), Ct);
        await Client.CloseAsync(Request(Clock.Today, Counted(rice.Id, counter.Id, 100)), Ct);

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.DailyClosings.Reopen, day1.Id),
            new ReopenDailyClosingRequest { Reason = "Error" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Reopen_ShouldReturn400_WhenThereIsNoReason()
    {
        var (rice, counter) = await RiceAtTheCounterAsync(Clock.Today);
        var closing = await Client.CloseAsync(Request(Clock.Today, Counted(rice.Id, counter.Id, 100)), Ct);

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.DailyClosings.Reopen, closing.Id),
            new ReopenDailyClosingRequest { Reason = "" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ShouldListTheClosings_AndHideTheReopenedOnes()
    {
        var (rice, counter) = await RiceAtTheCounterAsync(Clock.Today);
        var closing = await Client.CloseAsync(Request(Clock.Today, Counted(rice.Id, counter.Id, 100)), Ct);
        await Client.PostAsJsonAsync(Route(ApiEndpoints.DailyClosings.Reopen, closing.Id),
            new ReopenDailyClosingRequest { Reason = "Error" }, Ct);
        await Client.CloseAsync(Request(Clock.Today, Counted(rice.Id, counter.Id, 100)), Ct);

        var confirmed = (await Client.GetFromJsonAsync<DailyClosingsResponse>(ApiEndpoints.DailyClosings.GetAll, Ct))!;
        var all = (await Client.GetFromJsonAsync<DailyClosingsResponse>(
            $"{ApiEndpoints.DailyClosings.GetAll}?includeVoided=true", Ct))!;

        confirmed.Total.Should().Be(1);
        confirmed.Items.Single().Status.Should().Be(DailyClosingState.Confirmed);
        all.Total.Should().Be(2);
    }
}
