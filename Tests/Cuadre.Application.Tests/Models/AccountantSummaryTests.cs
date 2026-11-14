using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

// D-62 and D-63. Every expected number is worked out by hand in the comment next to it.

public class TaxSuggestionTests
{
    private static Tax NewTax(string name, int rateBps, TaxBase taxBase, bool active = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        RateBps = rateBps,
        Base = taxBase,
        IsActive = active
    };

    [Fact]
    public void For_ShouldTakeEachTaxOverItsOwnBase()
    {
        // Sales 25,000; profit 7,000. 10 % of sales = 2,500; 35 % of profit = 2,450.
        var suggestions = TaxSuggestion.For(
            [NewTax("ONAT", 1_000, TaxBase.Sales), NewTax("Utilidades", 3_500, TaxBase.Profit)],
            2_500_000, 700_000);

        suggestions.Should().HaveCount(2);
        suggestions[0].Should().BeEquivalentTo(new { BaseCents = 2_500_000L, AmountCents = 250_000L });
        suggestions[1].Should().BeEquivalentTo(new { BaseCents = 700_000L, AmountCents = 245_000L });
    }

    [Fact]
    public void For_ShouldNotTaxAProfit_WhenThereWasALoss()
    {
        var suggestion = TaxSuggestion.For([NewTax("Utilidades", 3_500, TaxBase.Profit)], 2_500_000, -100_000)
            .Should().ContainSingle().Subject;

        suggestion.BaseCents.Should().Be(0);
        suggestion.AmountCents.Should().Be(0);
    }

    [Fact]
    public void For_ShouldLeaveOutTheInactiveTaxes()
    {
        TaxSuggestion.For([NewTax("Vieja", 1_000, TaxBase.Sales, active: false)], 2_500_000, 700_000)
            .Should().BeEmpty();
    }

    [Fact]
    public void For_ShouldRoundToTheCent()
    {
        // 12.5 % of 0.99 CUP = 0.12375 → 0.12.
        TaxSuggestion.For([NewTax("Rara", 1_250, TaxBase.Sales)], 99, 0).Single().AmountCents.Should().Be(12);
    }
}

public class AccountantConversionTests
{
    private static readonly Guid Freight = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 11, 10);

    private static ExpenseLine Line(Currency currency, long amountCents, long? rateCents = null) => new()
    {
        CategoryId = Freight,
        CategoryName = "Transporte",
        Date = Day,
        Currency = currency,
        AmountCents = amountCents,
        RateCents = rateCents
    };

    [Fact]
    public void ToCupCents_ShouldMultiplyByTheRate()
    {
        Exchange.ToCupCents(1_000, 32_500).Should().Be(325_000);   // 10 USD × 325 = 3,250 CUP
    }

    [Fact]
    public void Summarize_ShouldKeepEachCurrencyApart_AndConvertTheUsdAtItsOwnRate()
    {
        // 500 CUP + 10 USD at 325 + 2 USD at 330: 3,250 + 660 = 3,910 in CUP. Total 4,410.
        var total = ConvertedExpenseTotal.Summarize([
            Line(Currency.CUP, 50_000), Line(Currency.USD, 1_000, 32_500), Line(Currency.USD, 200, 33_000)
        ]).Should().ContainSingle().Subject;

        total.CupCents.Should().Be(50_000);
        total.UsdCents.Should().Be(1_200);
        total.UsdAsCupCents.Should().Be(391_000);
        total.TotalCupCents.Should().Be(441_000);
        total.UsdWithoutRateCents.Should().Be(0);
    }

    [Fact]
    public void Summarize_ShouldLeaveOut_TheUsdWithoutARate()
    {
        var total = ConvertedExpenseTotal.Summarize([Line(Currency.USD, 1_000)]).Single();

        total.UsdCents.Should().Be(1_000);
        total.UsdAsCupCents.Should().Be(0);
        total.UsdWithoutRateCents.Should().Be(1_000);
        total.TotalCupCents.Should().Be(0);
    }

    [Fact]
    public void PurchaseTotals_ShouldConvertEachPurchase_AtItsOwnRate()
    {
        // Purchase A: 40 USD of goods + 500 CUP of freight, at 325. Purchase B: 10 USD at 340.
        // USD 50; in CUP 13,000 + 3,400 = 16,400; CUP 500. Total 16,900.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var totals = PurchaseTotals.Summarize([
            new PurchaseAmountRow { PurchaseId = a, Currency = Currency.USD, AmountCents = 4_000, RateCents = 32_500 },
            new PurchaseAmountRow { PurchaseId = a, Currency = Currency.CUP, AmountCents = 50_000, RateCents = 32_500 },
            new PurchaseAmountRow { PurchaseId = b, Currency = Currency.USD, AmountCents = 1_000, RateCents = 34_000 }
        ]);

        totals.Count.Should().Be(2);
        totals.UsdCents.Should().Be(5_000);
        totals.UsdAsCupCents.Should().Be(1_640_000);
        totals.CupCents.Should().Be(50_000);
        totals.TotalCupCents.Should().Be(1_690_000);
    }
}

public class InventoryValuationTests
{
    private static readonly DateOnly Day = new(2026, 11, 10);
    private static readonly Guid Storage = Guid.NewGuid();
    private static readonly Guid Counter = Guid.NewGuid();

    private static InventoryRow Row(Guid productId, Guid locationId, string name, long units) => new()
    {
        ProductId = productId,
        LocationId = locationId,
        LocationName = name,
        QuantityMilli = units * 1_000
    };

    [Fact]
    public void ByLocation_ShouldShareTheBatches_ByQuantity_WithoutLosingACent()
    {
        // 2 bottles at 200 + 1 at 250 = 650 CUP for 3 bottles. 1 in the storage, 2 on the counter:
        // storage 650 / 3 = 216.67; the counter takes the rest, 433.33. Together 650.
        var oil = Guid.NewGuid();
        var book = new FifoBook();
        book.Put(2_000, new UnitCost(1_000_000, 200_000_000), Guid.NewGuid(), Day);
        book.Put(1_000, new UnitCost(1_000_000, 250_000_000), Guid.NewGuid(), Day);

        var values = InventoryValuation.ByLocation(
            [Row(oil, Storage, "Almacén", 1), Row(oil, Counter, "Mostrador", 2)],
            new Dictionary<Guid, FifoBook> { [oil] = book });

        values.Should().BeEquivalentTo(new[]
        {
            new LocationValue(Storage, "Almacén", 21_667),
            new LocationValue(Counter, "Mostrador", 43_333)
        });
    }

    [Fact]
    public void ByLocation_ShouldAddTheProductsOfALocation()
    {
        // Rice 10 lb at 130 = 1,300; oil 2 bottles at 200 = 400. Storage: 1,700.
        var rice = Guid.NewGuid();
        var oil = Guid.NewGuid();
        var riceBook = new FifoBook();
        riceBook.Put(10_000, new UnitCost(400_000, 130_000_000), Guid.NewGuid(), Day);
        var oilBook = new FifoBook();
        oilBook.Put(2_000, new UnitCost(1_000_000, 200_000_000), Guid.NewGuid(), Day);

        var values = InventoryValuation.ByLocation(
            [Row(rice, Storage, "Almacén", 10), Row(oil, Storage, "Almacén", 2)],
            new Dictionary<Guid, FifoBook> { [rice] = riceBook, [oil] = oilBook });

        values.Should().ContainSingle().Which.ValueCents.Should().Be(170_000);
    }

    [Fact]
    public void ByLocation_ShouldBeWorthNothing_WhenNeverBought()
    {
        var found = Guid.NewGuid();

        InventoryValuation.ByLocation([Row(found, Storage, "Almacén", 3)], new Dictionary<Guid, FifoBook>())
            .Should().ContainSingle().Which.ValueCents.Should().Be(0);
    }

    [Fact]
    public void Join_ShouldPutTheOpeningAndTheClosingSideBySide()
    {
        var joined = LocationInventory.Join(
            [new LocationValue(Storage, "Almacén", 100_000)],
            [new LocationValue(Storage, "Almacén", 40_000), new LocationValue(Counter, "Mostrador", 25_000)]);

        joined.Should().BeEquivalentTo(new[]
        {
            new LocationInventory(Storage, "Almacén", 100_000, 40_000),
            new LocationInventory(Counter, "Mostrador", 0, 25_000)
        });
    }
}

public class AccountantSummaryTests
{
    private static readonly Tax Onat = new()
    {
        Id = Guid.NewGuid(),
        Name = "ONAT",
        RateBps = 1_000,
        Base = TaxBase.Profit
    };

    private static AccountantSummary Month(long costCents, long bufferCents, long lossCents = 0, long gainCents = 0) =>
        AccountantSummary.Create(2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), 20,
            [new IncomeByMethod(Guid.NewGuid(), "Efectivo", false, 2_000_000),
             new IncomeByMethod(Guid.NewGuid(), "Transfermóvil", true, 500_000)],
            new CostOfSalesSplit { CostCents = costCents, BufferCents = bufferCents },
            [new ConvertedExpenseTotal(Guid.NewGuid(), "Transporte", 50_000, 1_000, 325_000, 0)],
            125_000, lossCents, gainCents, 30_000, [Onat], new PurchaseTotals(0, 0, 0, 0), []);

    [Fact]
    public void Create_ShouldCostTheSales_WithoutTheColchon()
    {
        // A2 13,800 = 13,000 real + 800 colchón. Income 25,000 − 13,000 = 12,000 gross.
        var summary = Month(costCents: 1_380_000, bufferCents: 80_000);

        summary.IncomeCents.Should().Be(2_500_000);
        summary.CostOfSalesCents.Should().Be(1_300_000);
        summary.BufferCents.Should().Be(80_000);
        summary.GrossProfitCents.Should().Be(1_200_000);
    }

    [Fact]
    public void Create_ShouldSubtractExpensesStaffAndLosses_ButNotTheCashShortages()
    {
        // 12,000 − 3,750 expenses − 1,250 staff − 400 lost goods + 100 found = 6,700. The 300 short are already
        // missing from the income: they are not subtracted again.
        // 10 % ONAT over 6,700 = 670 → 6,030 after taxes.
        var summary = Month(costCents: 1_380_000, bufferCents: 80_000, lossCents: 40_000, gainCents: 10_000);

        summary.ExpensesCents.Should().Be(375_000);
        summary.ProfitBeforeTaxesCents.Should().Be(670_000);
        summary.LostShortageCents.Should().Be(30_000);
        summary.Taxes.Should().ContainSingle().Which.AmountCents.Should().Be(67_000);
        summary.TaxesCents.Should().Be(67_000);
        summary.ProfitAfterTaxesCents.Should().Be(603_000);
    }
}
