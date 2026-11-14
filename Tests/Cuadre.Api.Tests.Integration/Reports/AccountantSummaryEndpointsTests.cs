using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.CashBoxes;
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

// D-63. Today is Thursday 2026-11-12. Seeded colchón 0, so A2 is the real FIFO cost.
// The rice: 1 sack = 100 lb at USD 40 and 325 → 13,000 CUP the sack; sold at 250 CUP the lb.
// Selling the 100 lb: income 25,000 CUP, cost 13,000, staff 1,250 (1,000 + 1 % of 25,000).

[Collection(nameof(ApiCollection))]
public class AccountantSummaryEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private DateOnly Yesterday => Clock.Today.AddDays(-1);

    private async Task<AccountantSummaryResponse> SummaryAsync(string query = "")
    {
        var response = await Client.GetAsync(ApiEndpoints.Reports.Accountant + query, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<AccountantSummaryResponse>(Ct))!;
    }

    private async Task SetRateAsync(DateOnly date, long cupPerUsdCents)
    {
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.ExchangeRates.Upsert, date),
            new UpsertExchangeRateRequest { CupPerUsdCents = cupPerUsdCents }, Ct);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task CreateTaxAsync(string name, int rateBps, TaxBaseCode taxBase, bool active = true)
    {
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Taxes.Create,
            new CreateTaxRequest { Name = name, RateBps = rateBps, Base = taxBase }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        if (!active)
        {
            var tax = (await response.Content.ReadFromJsonAsync<TaxResponse>(Ct))!;
            (await Client.PutAsJsonAsync(Route(ApiEndpoints.Taxes.Update, tax.Id),
                new UpdateTaxRequest { Name = name, RateBps = rateBps, Base = taxBase, IsActive = false }, Ct))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    private async Task CreateExpenseAsync(long amountCents, string cashBox)
    {
        var categoryId = (await Client.GetFromJsonAsync<ExpenseCategoriesResponse>(
            ApiEndpoints.ExpenseCategories.GetAll, Ct))!.Items.Single(x => x.Name == "Transporte").Id;
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Expenses.Create, new CreateExpenseRequest
        {
            Date = Clock.Today,
            CategoryId = categoryId,
            Description = "Flete",
            AmountCents = amountCents,
            CashBoxId = (await Client.GetCashBoxByNameAsync(cashBox, Ct)).Id
        }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>Buys a sack yesterday, moves it to the counter and sells all of it, collected in cash.</summary>
    private async Task SellASackYesterdayAsync()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Yesterday, Ct, Sacks(rice.Id, 1, 4_000)), Ct);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);
        await Client.TransferAsync(Yesterday, storage.Id, counter.Id, Ct, SacksOf(rice.Id, 1));
        var draft = await Client.GetDraftAsync(Yesterday, Ct);
        await Client.CloseAsync(new CreateDailyClosingRequest
        {
            Date = Yesterday,
            Counts = [Counted(rice.Id, counter.Id, 0)],
            Collections = [draft.Collected("Efectivo", 2_500_000)]
        }, Ct);
    }

    [Fact]
    public async Task Summary_ShouldBeEmpty_BeforeAnything()
    {
        var summary = await SummaryAsync();

        summary.Year.Should().Be(2026);
        summary.Month.Should().Be(11);
        summary.From.Should().Be(new DateOnly(2026, 11, 1));
        summary.To.Should().Be(Clock.Today);
        summary.IncomeCents.Should().Be(0);
        summary.Taxes.Should().BeEmpty();
        summary.Inventory.Should().BeEmpty();
    }

    [Fact]
    public async Task Summary_ShouldBuildTheEstadoDeRendimiento_WithTheUsdAtTheRateOfItsDay()
    {
        // Rate typed yesterday only: today's USD expense takes the last rate before it (325).
        await SetRateAsync(Yesterday, Rate325);
        await SellASackYesterdayAsync();
        await CreateExpenseAsync(50_000, "Efectivo CUP");    // 500 CUP
        await CreateExpenseAsync(1_000, "Efectivo USD");     // 10 USD × 325 = 3,250 CUP
        await CreateTaxAsync("ONAT", 1_000, TaxBaseCode.Sales);
        await CreateTaxAsync("Utilidades", 3_500, TaxBaseCode.Profit);
        await CreateTaxAsync("Vieja", 5_000, TaxBaseCode.Sales, active: false);

        var summary = await SummaryAsync();

        summary.ClosedDays.Should().Be(1);
        summary.Income.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Name = "Efectivo", IsTransfer = false, AmountCents = 2_500_000L });
        summary.CostOfSalesCents.Should().Be(1_300_000);
        summary.BufferCents.Should().Be(0);
        summary.GrossProfitCents.Should().Be(1_200_000);

        var freight = summary.Expenses.Should().ContainSingle().Subject;
        freight.CupCents.Should().Be(50_000);
        freight.UsdCents.Should().Be(1_000);
        freight.UsdAsCupCents.Should().Be(325_000);
        freight.TotalCupCents.Should().Be(375_000);
        summary.UsdWithoutRateCents.Should().Be(0);
        summary.StaffPayCents.Should().Be(125_000);

        // 12,000 − 3,750 − 1,250 = 7,000. ONAT 10 % of 25,000 = 2,500; Utilidades 35 % of 7,000 = 2,450.
        summary.ProfitBeforeTaxesCents.Should().Be(700_000);
        summary.Taxes.Select(tax => (tax.Name, tax.AmountCents)).Should().BeEquivalentTo(new[]
        {
            ("ONAT", 250_000L), ("Utilidades", 245_000L)
        });
        summary.TaxesCents.Should().Be(495_000);
        summary.ProfitAfterTaxesCents.Should().Be(205_000);

        summary.Purchases.Should().BeEquivalentTo(new
        {
            Count = 1, CupCents = 0L, UsdCents = 4_000L, UsdAsCupCents = 1_300_000L, TotalCupCents = 1_300_000L
        });
    }

    [Fact]
    public async Task Summary_ShouldLeaveOutTheUsd_WhenNoRateWasEverTyped()
    {
        await CreateExpenseAsync(1_000, "Efectivo USD");

        var summary = await SummaryAsync();

        summary.ExpensesCents.Should().Be(0);
        summary.UsdWithoutRateCents.Should().Be(1_000);
    }

    [Fact]
    public async Task Summary_ShouldValueTheShelves_AtTheStartAndAtTheEnd_ByLocation()
    {
        // October: a sack at USD 40 (13,000). Yesterday: a sack at USD 50 (16,250), and one sack to the counter.
        // Start (31 Oct): storage 13,000. End: 200 lb worth 29,250, 100 lb in each place → 14,625 each.
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        await Client.CreatePurchaseAsync(await Client.UsdPurchaseRequestAsync(supplier.Id, new DateOnly(2026, 10, 20),
            Ct, Sacks(rice.Id, 1, 4_000)), Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Yesterday, Ct, Sacks(rice.Id, 1, 5_000)), Ct);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);
        await Client.TransferAsync(Yesterday, storage.Id, counter.Id, Ct, SacksOf(rice.Id, 1));

        var summary = await SummaryAsync();

        summary.Inventory.Should().BeEquivalentTo(new[]
        {
            new { LocationId = storage.Id, OpeningCents = 1_300_000L, ClosingCents = 1_462_500L },
            new { LocationId = counter.Id, OpeningCents = 0L, ClosingCents = 1_462_500L }
        });
        summary.OpeningInventoryCents.Should().Be(1_300_000);
        summary.ClosingInventoryCents.Should().Be(2_925_000);
        summary.Purchases.Count.Should().Be(1);   // only November's
    }

    [Fact]
    public async Task Summary_ShouldRefuseAMonthThatHasNotStarted()
    {
        var response = await Client.GetAsync(ApiEndpoints.Reports.Accountant + "?year=2026&month=12", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
