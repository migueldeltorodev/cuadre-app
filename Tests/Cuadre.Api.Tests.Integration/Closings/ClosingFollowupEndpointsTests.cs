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

// Slice 6b. The same rice as DailyClosingEndpointsTests: 100 lb at the counter, 250 CUP the lb, 2% of shrinkage.
// Seeded plan: Ganancia 5%, Dueño 50%, Impuestos 15%, Operación 30%.

[Collection(nameof(ApiCollection))]
public class ClosingFollowupEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const long PricePerLb = 25_000;
    private const long Shrinkage = 50_000;   // 2% of 25,000 CUP of sales

    private async Task<(ProductResponse Rice, LocationResponse Counter)> RiceAtTheCounterAsync(DateOnly date)
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct, PricePerLb);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, date, Ct, Sacks(rice.Id, 1, 4_000)), Ct);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);
        await Client.TransferAsync(date, storage.Id, counter.Id, Ct, SacksOf(rice.Id, 1));
        return (rice, counter);
    }

    private static CreateDailyClosingRequest Request(DateOnly date, ClosingCountRequest count, string? notes,
        params ClosingCollectionRequest[] collections) => new()
    {
        Date = date,
        Notes = notes,
        Counts = [count],
        Collections = collections
    };

    /// <summary>Yesterday everything was sold (25,000) but <paramref name="shortageCents"/> beyond the shrinkage is missing.</summary>
    private async Task<DailyClosingResponse> ShortageYesterdayAsync(long shortageCents)
    {
        var yesterday = Clock.Today.AddDays(-1);
        var (rice, counter) = await RiceAtTheCounterAsync(yesterday);
        var draft = await Client.GetDraftAsync(yesterday, Ct);
        var closing = await Client.CloseAsync(Request(yesterday, Counted(rice.Id, counter.Id, 0), "Faltó plata",
            draft.Collected("Efectivo", 2_500_000 - Shrinkage - shortageCents)), Ct);
        closing.Discrepancy!.AmountCents.Should().Be(shortageCents);
        return closing;
    }

    private async Task<HttpResponseMessage> RecoverAsync(Guid discrepancyId, long amountCents, DateOnly? date = null)
    {
        var cash = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        return await Client.PostAsJsonAsync(Route(ApiEndpoints.CashDiscrepancies.Recover, discrepancyId),
            new RecoverShortageRequest { Date = date, CashBoxId = cash.Id, AmountCents = amountCents }, Ct);
    }

    private static async Task<CashDiscrepancyResponse> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
        return (await response.Content.ReadFromJsonAsync<CashDiscrepancyResponse>(ct))!;
    }

    // ---------- Recover (RF-CIE-13) ----------

    [Fact]
    public async Task Recover_ShouldSplitTheMoneyLikeAnA3_AndCloseTheShortageWhenComplete()
    {
        var closing = await ShortageYesterdayAsync(100_000);
        var cash = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        var before = await Client.GetOverviewAsync(Ct);

        // 400 CUP back today: 5/50/15/30 → 20 / 200 / 60 / 120. Nothing for Reposición.
        var partial = await ReadAsync(await RecoverAsync(closing.Discrepancy!.Id, 40_000), Ct);

        partial.Status.Should().Be(CashDiscrepancyState.Pending);
        partial.RecoveredCents.Should().Be(40_000);
        partial.PendingCents.Should().Be(60_000);
        var recovery = partial.Recoveries.Should().ContainSingle().Subject;
        recovery.Date.Should().Be(Clock.Today);
        recovery.Status.Should().Be(ShortageRecoveryState.Confirmed);

        var after = await Client.GetOverviewAsync(Ct);
        (after.Balance(EnvelopeType.Profit, cash.Id) - before.Balance(EnvelopeType.Profit, cash.Id)).Should().Be(2_000);
        (after.Balance(EnvelopeType.OwnerPay, cash.Id) - before.Balance(EnvelopeType.OwnerPay, cash.Id))
            .Should().Be(20_000);
        (after.Balance(EnvelopeType.Tax, cash.Id) - before.Balance(EnvelopeType.Tax, cash.Id)).Should().Be(6_000);
        (after.Balance(EnvelopeType.Operations, cash.Id) - before.Balance(EnvelopeType.Operations, cash.Id))
            .Should().Be(12_000);
        (after.Balance(EnvelopeType.Replenishment, cash.Id) - before.Balance(EnvelopeType.Replenishment, cash.Id))
            .Should().Be(0);

        // The rest
        var complete = await ReadAsync(await RecoverAsync(closing.Discrepancy.Id, 60_000), Ct);

        complete.Status.Should().Be(CashDiscrepancyState.Recovered);
        complete.PendingCents.Should().Be(0);
        complete.ResolvedAt.Should().NotBeNull();
        complete.Recoveries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Recover_ShouldReturn422_WhenItGoesBeyondWhatIsLeft()
    {
        var closing = await ShortageYesterdayAsync(100_000);

        var response = await RecoverAsync(closing.Discrepancy!.Id, 100_001);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Recover_ShouldReturn422_WhenTheDayIsClosed()
    {
        var closing = await ShortageYesterdayAsync(100_000);

        // Yesterday is closed: the money can only come in on an open day.
        var response = await RecoverAsync(closing.Discrepancy!.Id, 10_000, Clock.Today.AddDays(-1));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Recover_ShouldReturn400_WhenTheAmountIsNotPositive()
    {
        var closing = await ShortageYesterdayAsync(100_000);

        var response = await RecoverAsync(closing.Discrepancy!.Id, 0);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Recover_ShouldReturn404_WhenTheShortageDoesNotExist()
    {
        var response = await RecoverAsync(Guid.NewGuid(), 10_000);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recover_ShouldReturn422_ForASurplus()
    {
        var yesterday = Clock.Today.AddDays(-1);
        var (rice, counter) = await RiceAtTheCounterAsync(yesterday);
        var draft = await Client.GetDraftAsync(yesterday, Ct);
        var closing = await Client.CloseAsync(Request(yesterday, Counted(rice.Id, counter.Id, 0), "Sobró",
            draft.Collected("Efectivo", 2_600_000)), Ct);
        closing.Discrepancy!.Kind.Should().Be(CashDiscrepancyType.Surplus);

        var response = await RecoverAsync(closing.Discrepancy.Id, 10_000);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ---------- Void a recovery ----------

    [Fact]
    public async Task VoidRecovery_ShouldTakeTheMoneyBack_AndLeaveTheShortagePending()
    {
        var closing = await ShortageYesterdayAsync(100_000);
        var cash = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        var before = await Client.GetOverviewAsync(Ct);
        var recovered = await ReadAsync(await RecoverAsync(closing.Discrepancy!.Id, 100_000), Ct);

        var response = await Client.PostAsJsonAsync(
            Route(ApiEndpoints.ShortageRecoveries.Void, recovered.Recoveries.Single().Id),
            new VoidShortageRecoveryRequest { Reason = "Se cargó dos veces" }, Ct);

        var voided = await ReadAsync(response, Ct);
        voided.Status.Should().Be(CashDiscrepancyState.Pending);
        voided.RecoveredCents.Should().Be(0);
        voided.PendingCents.Should().Be(100_000);
        voided.ResolvedAt.Should().BeNull();
        var recovery = voided.Recoveries.Single();
        recovery.Status.Should().Be(ShortageRecoveryState.Voided);
        recovery.VoidReason.Should().Be("Se cargó dos veces");

        (await Client.GetOverviewAsync(Ct)).BoxTotal(cash.Id).Should().Be(before.BoxTotal(cash.Id));

        // Twice: no
        var again = await Client.PostAsJsonAsync(
            Route(ApiEndpoints.ShortageRecoveries.Void, recovery.Id),
            new VoidShortageRecoveryRequest { Reason = "Otra vez" }, Ct);
        again.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Reopen_ShouldReturn422_WhileTheShortageHasRecoveries()
    {
        var closing = await ShortageYesterdayAsync(100_000);
        var recovered = await ReadAsync(await RecoverAsync(closing.Discrepancy!.Id, 10_000), Ct);

        var blocked = await Client.PostAsJsonAsync(Route(ApiEndpoints.DailyClosings.Reopen, closing.Id),
            new ReopenDailyClosingRequest { Reason = "Error" }, Ct);
        blocked.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await Client.PostAsJsonAsync(Route(ApiEndpoints.ShortageRecoveries.Void, recovered.Recoveries.Single().Id),
            new VoidShortageRecoveryRequest { Reason = "Para reabrir" }, Ct);

        var reopened = await Client.PostAsJsonAsync(Route(ApiEndpoints.DailyClosings.Reopen, closing.Id),
            new ReopenDailyClosingRequest { Reason = "Error" }, Ct);
        reopened.StatusCode.Should().Be(HttpStatusCode.OK, await reopened.Content.ReadAsStringAsync(Ct));
    }

    // ---------- Write off ----------

    [Fact]
    public async Task WriteOff_ShouldMarkItLost_AndRefuseMoreRecoveries()
    {
        var closing = await ShortageYesterdayAsync(100_000);
        await RecoverAsync(closing.Discrepancy!.Id, 30_000);

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.CashDiscrepancies.WriteOff, closing.Discrepancy.Id),
            new WriteOffShortageRequest { Note = "No apareció" }, Ct);

        var lost = await ReadAsync(response, Ct);
        lost.Status.Should().Be(CashDiscrepancyState.Lost);
        lost.ResolutionNote.Should().Be("No apareció");
        lost.RecoveredCents.Should().Be(30_000);
        lost.PendingCents.Should().Be(0);

        (await RecoverAsync(closing.Discrepancy.Id, 10_000)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task WriteOff_ShouldReturn400_WithoutANote()
    {
        var closing = await ShortageYesterdayAsync(100_000);

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.CashDiscrepancies.WriteOff, closing.Discrepancy!.Id),
            new WriteOffShortageRequest { Note = "" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ShouldFilterTheShortages_AndSumWhatIsPending()
    {
        var closing = await ShortageYesterdayAsync(100_000);
        await RecoverAsync(closing.Discrepancy!.Id, 25_000);

        var pending = (await Client.GetFromJsonAsync<CashDiscrepanciesResponse>(
            $"{ApiEndpoints.CashDiscrepancies.GetAll}?kind=Shortage&status=Pending", Ct))!;
        var lost = (await Client.GetFromJsonAsync<CashDiscrepanciesResponse>(
            $"{ApiEndpoints.CashDiscrepancies.GetAll}?status=Lost", Ct))!;
        var summary = (await Client.GetFromJsonAsync<PendingShortagesResponse>(
            ApiEndpoints.CashDiscrepancies.PendingSummary, Ct))!;

        var item = pending.Items.Should().ContainSingle().Subject;
        item.ClosingId.Should().Be(closing.Id);
        item.Date.Should().Be(closing.Date);
        item.PendingCents.Should().Be(75_000);
        lost.Total.Should().Be(0);
        summary.Count.Should().Be(1);
        summary.PendingCents.Should().Be(75_000);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenItDoesNotExist()
    {
        var response = await Client.GetAsync(Route(ApiEndpoints.CashDiscrepancies.Get, Guid.NewGuid()), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Days left to close (RF-CIE-09) ----------

    [Fact]
    public async Task Pending_ShouldListTheDaysOverAndNotClosed()
    {
        // Activity three days ago; today is never pending (it is not over).
        var first = Clock.Today.AddDays(-3);
        var (rice, counter) = await RiceAtTheCounterAsync(first);

        var before = (await Client.GetFromJsonAsync<PendingClosingDaysResponse>(ApiEndpoints.DailyClosings.Pending, Ct))!;
        before.LastClosedDate.Should().BeNull();
        before.Count.Should().Be(3);
        before.Days.Should().Equal(first, first.AddDays(1), first.AddDays(2));

        await Client.CloseAsync(Request(first.AddDays(1), Counted(rice.Id, counter.Id, 100), null), Ct);

        var after = (await Client.GetFromJsonAsync<PendingClosingDaysResponse>(ApiEndpoints.DailyClosings.Pending, Ct))!;
        after.LastClosedDate.Should().Be(first.AddDays(1));
        after.Days.Should().Equal(Clock.Today.AddDays(-1));
    }

    // ---------- Duplicates (RF-CIE-11) ----------

    [Fact]
    public async Task Preview_ShouldWarn_WhenAnotherDayHasTheSameNumbers()
    {
        var yesterday = Clock.Today.AddDays(-1);
        var (rice, counter) = await RiceAtTheCounterAsync(yesterday);
        var draft1 = await Client.GetDraftAsync(yesterday, Ct);
        await Client.CloseAsync(Request(yesterday, Counted(rice.Id, counter.Id, 60), null,
            draft1.Collected("Efectivo", 1_000_000)), Ct);

        // Today: again 40 lb and 10,000 in cash, as if yesterday was loaded twice.
        var draft2 = await Client.GetDraftAsync(Clock.Today, Ct);
        var request = Request(Clock.Today, Counted(rice.Id, counter.Id, 20), null,
            draft2.Collected("Efectivo", 1_000_000));
        var preview = await Client.PostAsJsonAsync(ApiEndpoints.DailyClosings.Preview, request, Ct);
        var previewed = (await preview.Content.ReadFromJsonAsync<DailyClosingResponse>(Ct))!;

        previewed.PossibleDuplicateOf.Should().Be(yesterday);
        (await Client.CloseAsync(request, Ct)).PossibleDuplicateOf.Should().Be(yesterday);
    }

    [Fact]
    public async Task Preview_ShouldNotWarn_WhenTheNumbersDiffer()
    {
        var yesterday = Clock.Today.AddDays(-1);
        var (rice, counter) = await RiceAtTheCounterAsync(yesterday);
        var draft1 = await Client.GetDraftAsync(yesterday, Ct);
        await Client.CloseAsync(Request(yesterday, Counted(rice.Id, counter.Id, 60), null,
            draft1.Collected("Efectivo", 1_000_000)), Ct);

        var draft2 = await Client.GetDraftAsync(Clock.Today, Ct);
        var preview = await Client.PostAsJsonAsync(ApiEndpoints.DailyClosings.Preview,
            Request(Clock.Today, Counted(rice.Id, counter.Id, 30), null, draft2.Collected("Efectivo", 750_000)), Ct);

        (await preview.Content.ReadFromJsonAsync<DailyClosingResponse>(Ct))!.PossibleDuplicateOf.Should().BeNull();
    }

    // ---------- Evaluation (RF-PF-10) ----------

    [Fact]
    public async Task Evaluation_ShouldSumTheClosings_AndCompareTheSplitWithThePlan()
    {
        // Today: everything sold, 20,000 cash + 5,000 Transfermóvil. A1 25,000; A2 13,000; A3 12,000.
        var (rice, counter) = await RiceAtTheCounterAsync(Clock.Today);
        var draft = await Client.GetDraftAsync(Clock.Today, Ct);
        await Client.CloseAsync(Request(Clock.Today, Counted(rice.Id, counter.Id, 0), null,
            draft.Collected("Efectivo", 2_000_000), draft.Collected("Transfermóvil", 500_000)), Ct);

        var evaluation = (await Client.GetFromJsonAsync<ProfitFirstEvaluationResponse>(
            ApiEndpoints.ProfitFirst.Evaluation, Ct))!;

        evaluation.From.Should().Be(new DateOnly(Clock.Today.Year, Clock.Today.Month, 1));
        evaluation.To.Should().Be(Clock.Today);
        evaluation.ClosedDays.Should().Be(1);
        evaluation.CollectedCents.Should().Be(2_500_000);
        evaluation.CostOfSalesCents.Should().Be(1_300_000);
        evaluation.RealIncomeCents.Should().Be(1_200_000);
        evaluation.CostRatioBps.Should().Be(5_200);        // 13,000 / 25,000
        evaluation.RealIncomeRatioBps.Should().Be(4_800);
        evaluation.StaffPayCents.Should().Be(125_000);

        var profit = evaluation.Envelopes.Single(x => x.Role == EnvelopeType.Profit);
        profit.TargetPercentBps.Should().Be(500);
        profit.ReceivedCents.Should().Be(60_000);
        profit.RealPercentBps.Should().Be(500);
        var replenishment = evaluation.Envelopes.Single(x => x.Role == EnvelopeType.Replenishment);
        replenishment.TargetPercentBps.Should().BeNull();
        replenishment.ReceivedCents.Should().Be(1_300_000);
        replenishment.RealPercentBps.Should().BeNull();
    }

    [Fact]
    public async Task Evaluation_ShouldReturn400_WhenToIsBeforeFrom()
    {
        var response = await Client.GetAsync(
            $"{ApiEndpoints.ProfitFirst.Evaluation}?from=2026-11-10&to=2026-11-01", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
