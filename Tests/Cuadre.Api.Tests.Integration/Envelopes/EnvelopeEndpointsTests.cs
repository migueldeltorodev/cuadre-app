using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.CashBoxes;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Api.Tests.Integration.Inventory;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;
using static Cuadre.Api.Tests.Integration.Envelopes.EnvelopeTestHelpers;
using static Cuadre.Api.Tests.Integration.Inventory.InventoryTestHelpers;
using static Cuadre.Api.Tests.Integration.Purchases.PurchaseTestHelpers;

namespace Cuadre.Api.Tests.Integration.Envelopes;

// The rice of the purchase tests: one 100 lb sack at 62 USD from "Efectivo USD" → 201.50 CUP/lb at cost.

[Collection(nameof(ApiCollection))]
public class EnvelopeEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<ProductResponse> BuyRiceAsync(long sacks = 1)
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, sacks, 6_200)), Ct);
        return rice;
    }

    private Task<CashBoxResponse> CashCupAsync() => Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
    private Task<CashBoxResponse> BankCupAsync() => Client.GetCashBoxByNameAsync("Banco CUP", Ct);
    private Task<CashBoxResponse> CashUsdAsync() => Client.GetCashBoxByNameAsync("Efectivo USD", Ct);

    // ---------- Defaults ----------

    [Fact]
    public async Task Overview_ShouldStartWithTheProfitFirstEnvelopes_AndTheDefaultPercentages()
    {
        var overview = await Client.GetOverviewAsync(Ct);

        overview.Date.Should().Be(Clock.Today);
        overview.Envelopes.Select(x => x.Envelope.Role).Should().Equal(
            EnvelopeType.Profit, EnvelopeType.OwnerPay, EnvelopeType.Tax, EnvelopeType.Operations,
            EnvelopeType.Replenishment);
        overview.Envelopes.Select(x => x.PercentBps).Should().Equal(500, 5_000, 1_500, 3_000, null);
        overview.Envelope(EnvelopeType.Replenishment).TakesPercent.Should().BeFalse();
        overview.CashBoxes.Should().HaveCount(3).And.OnlyContain(x => x.TotalCents == 0);
        overview.CurrentPlan!.EffectiveFrom.Should().Be(new DateOnly(2000, 1, 1));
    }

    [Fact]
    public async Task Purchase_ShouldBePaidFromReposicion()
    {
        await BuyRiceAsync();
        var usd = await CashUsdAsync();

        var overview = await Client.GetOverviewAsync(Ct);

        overview.Balance(EnvelopeType.Replenishment, usd.Id).Should().Be(-6_200);
        overview.BoxTotal(usd.Id).Should().Be(-6_200);   // the box = the sum of its envelopes
    }

    // ---------- Opening ----------

    [Fact]
    public async Task Opening_ShouldSetEachEnvelopeToWhatIsPhysicallyThere_CountingThePastPurchases()
    {
        // Arrange: the app already says −62 USD in Reposición (the purchase)
        await BuyRiceAsync();
        var usd = await CashUsdAsync();
        var cup = await CashCupAsync();
        var overview = await Client.GetOverviewAsync(Ct);
        var replenishment = overview.Envelope(EnvelopeType.Replenishment);
        var owner = overview.Envelope(EnvelopeType.OwnerPay);

        // Act: there are 100 USD in Reposición and 5,000 CUP for the owner
        var operation = await Client.OpenAsync(Clock.Today, Ct,
            Target(usd.Id, replenishment.Id, 10_000), Target(cup.Id, owner.Id, 500_000));

        // Assert: the lines move the difference; the balances end at the targets
        operation.Kind.Should().Be(EnvelopeOperationType.Opening);
        operation.Number.Should().Be(1);
        operation.Lines.Should().HaveCount(2);
        operation.Lines.Single(x => x.CashBoxId == usd.Id).AmountCents.Should().Be(16_200);   // 100 − (−62)
        operation.Lines.Single(x => x.CashBoxId == usd.Id).TargetCents.Should().Be(10_000);
        operation.Lines.Single(x => x.CashBoxId == cup.Id).AmountCents.Should().Be(500_000);

        var after = await Client.GetOverviewAsync(Ct);
        after.Balance(EnvelopeType.Replenishment, usd.Id).Should().Be(10_000);
        after.Balance(EnvelopeType.OwnerPay, cup.Id).Should().Be(500_000);
        after.BoxTotal(usd.Id).Should().Be(10_000);
    }

    [Fact]
    public async Task Opening_ShouldReturn422_WhenNothingChanges()
    {
        var cup = await CashCupAsync();
        var profit = (await Client.GetOverviewAsync(Ct)).Envelope(EnvelopeType.Profit);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.EnvelopeOperations.CreateOpening,
            new CreateEnvelopeOpeningRequest { Date = Clock.Today, Targets = [Target(cup.Id, profit.Id, 0)] }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Opening_ShouldReturn400_WhenAnAmountIsNegative()
    {
        var cup = await CashCupAsync();
        var profit = (await Client.GetOverviewAsync(Ct)).Envelope(EnvelopeType.Profit);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.EnvelopeOperations.CreateOpening,
            new CreateEnvelopeOpeningRequest { Date = Clock.Today, Targets = [Target(cup.Id, profit.Id, -100)] }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- Transfers, withdrawals, taxes ----------

    [Fact]
    public async Task Transfer_ShouldMoveMoneyBetweenEnvelopes_WithoutChangingTheBox()
    {
        var cup = await CashCupAsync();
        var overview = await Client.GetOverviewAsync(Ct);
        var profit = overview.Envelope(EnvelopeType.Profit);
        var operations = overview.Envelope(EnvelopeType.Operations);
        await Client.OpenAsync(Clock.Today, Ct, Target(cup.Id, profit.Id, 100_000));

        var operation = await Client.MoveAsync(Clock.Today, cup.Id, profit.Id, operations.Id, 30_000, Ct);

        operation.EnvelopeName.Should().Be(profit.Name);
        operation.ToEnvelopeName.Should().Be(operations.Name);
        operation.Warnings.Should().BeEmpty();
        var after = await Client.GetOverviewAsync(Ct);
        after.Balance(profit.Id, cup.Id).Should().Be(70_000);
        after.Balance(operations.Id, cup.Id).Should().Be(30_000);
        after.BoxTotal(cup.Id).Should().Be(100_000);
    }

    [Fact]
    public async Task Withdrawal_ShouldBeAllowed_ButWarn_WhenTheEnvelopeGoesNegative()
    {
        var cup = await CashCupAsync();
        var owner = (await Client.GetOverviewAsync(Ct)).Envelope(EnvelopeType.OwnerPay);
        await Client.OpenAsync(Clock.Today, Ct, Target(cup.Id, owner.Id, 10_000));

        var operation = await Client.WithdrawAsync(Clock.Today, cup.Id, owner.Id, 25_000, Ct);

        var warning = operation.Warnings.Should().ContainSingle().Subject;
        warning.EnvelopeId.Should().Be(owner.Id);
        warning.CashBoxId.Should().Be(cup.Id);
        warning.Currency.Should().Be(CurrencyCode.CUP);
        warning.BalanceCents.Should().Be(-15_000);
        (await Client.GetOverviewAsync(Ct)).Balance(owner.Id, cup.Id).Should().Be(-15_000);
    }

    [Fact]
    public async Task TaxPayment_ShouldAlwaysLeaveFromImpuestos()
    {
        var bank = await BankCupAsync();
        var tax = (await Client.GetOverviewAsync(Ct)).Envelope(EnvelopeType.Tax);
        await Client.OpenAsync(Clock.Today, Ct, Target(bank.Id, tax.Id, 80_000));

        var operation = await Client.PostEnvelopeOperationAsync(ApiEndpoints.EnvelopeOperations.CreateTaxPayment,
            new CreateTaxPaymentRequest
            {
                Date = Clock.Today,
                CashBoxId = bank.Id,
                AmountCents = 50_000,
                Concept = "ONAT noviembre",
                Receipt = "A-1234"
            }, Ct);

        operation.Kind.Should().Be(EnvelopeOperationType.TaxPayment);
        operation.EnvelopeId.Should().Be(tax.Id);
        operation.Receipt.Should().Be("A-1234");
        (await Client.GetOverviewAsync(Ct)).Balance(tax.Id, bank.Id).Should().Be(30_000);
    }

    [Fact]
    public async Task TaxPayment_ShouldReturn400_WithoutAConcept()
    {
        var bank = await BankCupAsync();

        var response = await Client.PostAsJsonAsync(ApiEndpoints.EnvelopeOperations.CreateTaxPayment,
            new CreateTaxPaymentRequest { Date = Clock.Today, CashBoxId = bank.Id, AmountCents = 100, Concept = "" },
            Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Transfer_ShouldReturn400_WhenBothEnvelopesAreTheSame()
    {
        var cup = await CashCupAsync();
        var profit = (await Client.GetOverviewAsync(Ct)).Envelope(EnvelopeType.Profit);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.EnvelopeOperations.CreateTransfer,
            new CreateEnvelopeTransferRequest
            {
                Date = Clock.Today,
                CashBoxId = cup.Id,
                FromEnvelopeId = profit.Id,
                ToEnvelopeId = profit.Id,
                AmountCents = 100
            }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Void_ShouldReverseTheOperation_AndKeepItInTheHistory()
    {
        var cup = await CashCupAsync();
        var overview = await Client.GetOverviewAsync(Ct);
        var profit = overview.Envelope(EnvelopeType.Profit);
        var owner = overview.Envelope(EnvelopeType.OwnerPay);
        await Client.OpenAsync(Clock.Today, Ct, Target(cup.Id, profit.Id, 100_000));
        var move = await Client.MoveAsync(Clock.Today, cup.Id, profit.Id, owner.Id, 40_000, Ct);

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.EnvelopeOperations.Void, move.Id),
            new VoidEnvelopeOperationRequest { Reason = "Era otro sobre" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var voided = (await response.Content.ReadFromJsonAsync<EnvelopeOperationResponse>(Ct))!;
        voided.Status.Should().Be(EnvelopeOperationState.Voided);
        voided.VoidReason.Should().Be("Era otro sobre");

        var after = await Client.GetOverviewAsync(Ct);
        after.Balance(profit.Id, cup.Id).Should().Be(100_000);
        after.Balance(owner.Id, cup.Id).Should().Be(0);

        var again = await Client.PostAsJsonAsync(Route(ApiEndpoints.EnvelopeOperations.Void, move.Id),
            new VoidEnvelopeOperationRequest { Reason = "Otra vez" }, Ct);
        again.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var history = (await Client.GetFromJsonAsync<EnvelopeOperationsResponse>(
            ApiEndpoints.EnvelopeOperations.GetAll + "?includeVoided=true", Ct))!;
        history.Total.Should().Be(2);
        var confirmedOnly = (await Client.GetFromJsonAsync<EnvelopeOperationsResponse>(
            ApiEndpoints.EnvelopeOperations.GetAll, Ct))!;
        confirmedOnly.Items.Should().ContainSingle(x => x.Kind == EnvelopeOperationType.Opening);
    }

    // ---------- Ledger ----------

    [Fact]
    public async Task Movements_ShouldListTheEnvelopeRows_WithTheRunningBalancePerBox()
    {
        var cup = await CashCupAsync();
        var overview = await Client.GetOverviewAsync(Ct);
        var owner = overview.Envelope(EnvelopeType.OwnerPay);
        await Client.OpenAsync(Clock.Today, Ct, Target(cup.Id, owner.Id, 50_000));
        await Client.WithdrawAsync(Clock.Today, cup.Id, owner.Id, 20_000, Ct);

        var ledger = (await Client.GetFromJsonAsync<EnvelopeLedgerResponse>(
            Route(ApiEndpoints.Envelopes.Movements, owner.Id), Ct))!;

        ledger.Entries.Select(x => x.Movement.Kind).Should().Equal(CashMovementType.Opening, CashMovementType.Withdrawal);
        ledger.Entries.Select(x => x.BoxBalanceCents).Should().Equal(50_000, 30_000);
        ledger.Entries[1].Movement.EnvelopeOperationNumber.Should().Be(2);
        ledger.ClosingBalances.Should().ContainSingle().Which.AmountCents.Should().Be(30_000);
    }

    // ---------- House consumption (RN-11) ----------

    [Fact]
    public async Task HouseConsumption_ShouldMoveItsCostFromDuenoToReposicion_AndVoidingShouldUndoIt()
    {
        // Arrange
        var rice = await BuyRiceAsync();
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);
        var cup = await CashCupAsync();

        // Act: 2 lb taken home, at 201.50 CUP/lb
        var exit = await Client.ExitAsync(Clock.Today, storage.Id, ExitReasonCode.HouseConsumption, Ct,
            Pounds(rice.Id, 2));

        // Assert
        var overview = await Client.GetOverviewAsync(Ct);
        overview.Balance(EnvelopeType.OwnerPay, cup.Id).Should().Be(-40_300);
        overview.Balance(EnvelopeType.Replenishment, cup.Id).Should().Be(40_300);
        overview.BoxTotal(cup.Id).Should().Be(0);   // no cash moved

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.StockOperations.Void, exit.Id),
            new VoidStockOperationRequest { Reason = "Se devolvió" }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await Client.GetOverviewAsync(Ct);
        after.Balance(EnvelopeType.OwnerPay, cup.Id).Should().Be(0);
        after.Balance(EnvelopeType.Replenishment, cup.Id).Should().Be(0);
    }

    [Fact]
    public async Task ShrinkageExit_ShouldNotTouchTheEnvelopes()
    {
        var rice = await BuyRiceAsync();
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);
        var cup = await CashCupAsync();

        await Client.ExitAsync(Clock.Today, storage.Id, ExitReasonCode.Shrinkage, Ct, Pounds(rice.Id, 2));

        (await Client.GetOverviewAsync(Ct)).BoxTotal(cup.Id).Should().Be(0);
        (await Client.GetOverviewAsync(Ct)).Balance(EnvelopeType.OwnerPay, cup.Id).Should().Be(0);
    }

    // ---------- Allocation plans ----------

    private async Task<HttpResponseMessage> PostPlanAsync(DateOnly from, params (Guid EnvelopeId, int Bps)[] items) =>
        await Client.PostAsJsonAsync(ApiEndpoints.AllocationPlans.Create, new CreateAllocationPlanRequest
        {
            EffectiveFrom = from,
            Items = items.Select(x => new AllocationPlanItemRequest { EnvelopeId = x.EnvelopeId, PercentBps = x.Bps })
                .ToList()
        }, Ct);

    [Fact]
    public async Task Plan_ShouldApplyFromItsDate_AndKeepThePreviousOneForEarlierDays()
    {
        var overview = await Client.GetOverviewAsync(Ct);
        var profit = overview.Envelope(EnvelopeType.Profit).Id;
        var owner = overview.Envelope(EnvelopeType.OwnerPay).Id;
        var tax = overview.Envelope(EnvelopeType.Tax).Id;
        var operations = overview.Envelope(EnvelopeType.Operations).Id;

        var response = await PostPlanAsync(Clock.Today, (profit, 1_000), (owner, 4_500), (tax, 1_500),
            (operations, 3_000));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        (await Client.GetOverviewAsync(Ct)).Envelopes.Select(x => x.PercentBps)
            .Should().Equal(1_000, 4_500, 1_500, 3_000, null);
        var yesterday = Clock.Today.AddDays(-1).ToString("yyyy-MM-dd");
        (await Client.GetOverviewAsync(Ct, $"?date={yesterday}")).Envelopes.Select(x => x.PercentBps)
            .Should().Equal(500, 5_000, 1_500, 3_000, null);

        var plans = (await Client.GetFromJsonAsync<AllocationPlansResponse>(ApiEndpoints.AllocationPlans.GetAll, Ct))!;
        plans.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Plan_ShouldReplaceTheLatest_WhenItStartsTheSameDay()
    {
        var overview = await Client.GetOverviewAsync(Ct);
        var profit = overview.Envelope(EnvelopeType.Profit).Id;
        var owner = overview.Envelope(EnvelopeType.OwnerPay).Id;

        await PostPlanAsync(Clock.Today, (profit, 5_000), (owner, 5_000));
        var response = await PostPlanAsync(Clock.Today, (profit, 2_000), (owner, 8_000));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var plans = (await Client.GetFromJsonAsync<AllocationPlansResponse>(ApiEndpoints.AllocationPlans.GetAll, Ct))!;
        plans.Items.Should().HaveCount(2);
        var latest = plans.Items.First();
        latest.Items.Should().HaveCount(4);   // every envelope that takes a %, 0 included
        latest.Items.Single(x => x.EnvelopeId == owner).PercentBps.Should().Be(8_000);
    }

    [Fact]
    public async Task Plan_ShouldReturn400_WhenItDoesNotAddUpTo100()
    {
        var profit = (await Client.GetOverviewAsync(Ct)).Envelope(EnvelopeType.Profit).Id;

        var response = await PostPlanAsync(Clock.Today, (profit, 9_000));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Plan_ShouldReturn422_ForReposicion_OrADateBeforeTheLatestPlan()
    {
        var overview = await Client.GetOverviewAsync(Ct);
        var profit = overview.Envelope(EnvelopeType.Profit).Id;
        var replenishment = overview.Envelope(EnvelopeType.Replenishment).Id;

        (await PostPlanAsync(Clock.Today, (profit, 5_000), (replenishment, 5_000)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await PostPlanAsync(Clock.Today, (profit, 10_000));
        (await PostPlanAsync(Clock.Today.AddDays(-3), (profit, 10_000)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ---------- Envelopes ----------

    private async Task<EnvelopeResponse> CreateEnvelopeAsync(string name)
    {
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Envelopes.Create,
            new CreateEnvelopeRequest { Name = name }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<EnvelopeResponse>(Ct))!;
    }

    private Task<HttpResponseMessage> UpdateEnvelopeAsync(Guid id, string name, bool isActive) =>
        Client.PutAsJsonAsync(Route(ApiEndpoints.Envelopes.Update, id),
            new UpdateEnvelopeRequest { Name = name, IsActive = isActive }, Ct);

    [Fact]
    public async Task CreateEnvelope_ShouldAddACustomOne_WithoutPercent_AndRefuseADuplicateName()
    {
        var envelope = await CreateEnvelopeAsync("Ahorro reparaciones");

        envelope.Role.Should().Be(EnvelopeType.Custom);
        envelope.TakesPercent.Should().BeTrue();
        (await Client.GetOverviewAsync(Ct)).Envelopes.Single(x => x.Envelope.Id == envelope.Id).PercentBps
            .Should().Be(0);

        var duplicate = await Client.PostAsJsonAsync(ApiEndpoints.Envelopes.Create,
            new CreateEnvelopeRequest { Name = "ahorro REPARACIONES" }, Ct);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateEnvelope_ShouldRenameAProfitFirstEnvelope_ButNeverDeactivateIt()
    {
        var profit = (await Client.GetOverviewAsync(Ct)).Envelope(EnvelopeType.Profit);

        (await UpdateEnvelopeAsync(profit.Id, "Ganancia (no tocar)", true)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await UpdateEnvelopeAsync(profit.Id, "Ganancia (no tocar)", false))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateEnvelope_ShouldDeactivateACustomOne_OnlyWhenItIsEmpty()
    {
        var cup = await CashCupAsync();
        var envelope = await CreateEnvelopeAsync("Fondo de emergencia");
        var open = await Client.OpenAsync(Clock.Today, Ct, Target(cup.Id, envelope.Id, 1_000));

        (await UpdateEnvelopeAsync(envelope.Id, envelope.Name, false))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await Client.PostAsJsonAsync(Route(ApiEndpoints.EnvelopeOperations.Void, open.Id),
            new VoidEnvelopeOperationRequest { Reason = "Prueba" }, Ct);
        var response = await UpdateEnvelopeAsync(envelope.Id, envelope.Name, false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetOverviewAsync(Ct)).Envelopes.Should().NotContain(x => x.Envelope.Id == envelope.Id);
    }
}
