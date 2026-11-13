using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

// Every expected number is worked out by hand in the comment next to it.

public class InventoryMathTests
{
    private static readonly Guid Storage = Guid.NewGuid();
    private static readonly Guid Counter = Guid.NewGuid();

    [Fact]
    public void ToStockMilli_ShouldMultiplyByTheFactor_WhenTypedInPurchaseUnits()
    {
        // 2 sacks × 100 lb = 200 lb
        InventoryMath.ToStockMilli(2_000, inPurchaseUnits: true, 100_000).Should().Be(200_000);
    }

    [Fact]
    public void ToStockMilli_ShouldKeepTheQuantity_WhenTypedInStockUnits()
    {
        InventoryMath.ToStockMilli(37_500, inPurchaseUnits: false, 100_000).Should().Be(37_500);
    }

    [Fact]
    public void ToStockMilli_ShouldBeNull_WhenTheConversionIsNotExact()
    {
        // 0.001 × 1.5 = 0.0015
        InventoryMath.ToStockMilli(1, inPurchaseUnits: true, 1_500).Should().BeNull();
    }

    [Theory]
    [InlineData(150_000L, 100_000L, 2_000L)]   // 150 lb in 100 lb sacks → 2 sacks
    [InlineData(100_000L, 100_000L, 1_000L)]   // exactly one sack: no jump
    [InlineData(1L, 100_000L, 1_000L)]         // any shortage needs a whole sack
    [InlineData(0L, 100_000L, 0L)]
    [InlineData(-5_000L, 100_000L, 0L)]
    public void WholePurchaseUnitsFor_ShouldRoundUp(long stockMilli, long factorMilli, long expected)
    {
        InventoryMath.WholePurchaseUnitsFor(stockMilli, factorMilli).Should().Be(expected);
    }

    [Fact]
    public void RunningBalances_ShouldKeepABalancePerLocation_AndATotal()
    {
        // +200 storage, −50 storage, +50 counter → storage 200/150, counter 50, total 200/150/200
        var entries = InventoryMath.RunningBalances(
            [Move(Storage, 200_000), Move(Storage, -50_000), Move(Counter, 50_000)],
            new Dictionary<Guid, long>());

        entries.Select(entry => entry.LocationBalanceMilli).Should().Equal(200_000, 150_000, 50_000);
        entries.Select(entry => entry.TotalBalanceMilli).Should().Equal(200_000, 150_000, 200_000);
    }

    [Fact]
    public void RunningBalances_ShouldStartFromTheOpeningBalances()
    {
        var opening = new Dictionary<Guid, long> { [Storage] = 100_000, [Counter] = 10_000 };

        var entries = InventoryMath.RunningBalances([Move(Counter, -4_000)], opening);

        entries.Should().ContainSingle().Which.Should().Be(
            new KardexEntry(entries[0].Movement, LocationBalanceMilli: 6_000, TotalBalanceMilli: 106_000));
        opening[Counter].Should().Be(6_000);   // the caller reads the closing balances from it
    }

    [Fact]
    public void ShoppingListItem_ShouldSuggestWholeSacks_AndEstimateAtTheLastCost()
    {
        // Min 120 lb, stock 15 lb → missing 105 lb → 2 sacks (200 lb) × 0.60 USD = 120 USD
        var item = new ShoppingListItem
        {
            ConversionFactorMilli = 100_000,
            StockMilli = 15_000,
            MinStockMilli = 120_000,
            LastUnitCostUsdMicros = 600_000
        };

        item.MissingMilli.Should().Be(105_000);
        item.SuggestedPurchaseQuantityMilli.Should().Be(2_000);
        item.EstimatedUsdCents.Should().Be(12_000);
    }

    [Fact]
    public void StockOperationLine_ValueCupCents_ShouldBeQuantityTimesHistoricalCost()
    {
        // 10 lb at 201.50 CUP = 2,015.00 CUP; a shortage of 5 lb = −1,007.50
        Line(10_000, 201_500_000).ValueCupCents.Should().Be(201_500);
        Line(-5_000, 201_500_000).ValueCupCents.Should().Be(-100_750);
        Line(10_000, null).ValueCupCents.Should().BeNull();
    }

    [Fact]
    public void ProductStock_IsLow_OnlyOnceBought_AndBelowAPositiveMinimum()
    {
        Stock(total: 15_000, min: 20_000, bought: true).IsLow.Should().BeTrue();
        Stock(total: 20_000, min: 20_000, bought: true).IsLow.Should().BeFalse();
        Stock(total: 0, min: 20_000, bought: false).IsLow.Should().BeFalse();   // never stocked: not "low"
        Stock(total: -1_000, min: 0, bought: true).IsLow.Should().BeFalse();    // no minimum configured
    }

    private static StockMovementEntry Move(Guid locationId, long quantityMilli) => new()
    {
        Id = Guid.NewGuid(),
        LocationId = locationId,
        QuantityMilli = quantityMilli
    };

    private static StockOperationLine Line(long quantityMilli, long? cupMicros) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        EnteredQuantityMilli = Math.Abs(quantityMilli),
        EnteredInPurchaseUnits = false,
        QuantityMilli = quantityMilli,
        UnitCostCupMicros = cupMicros
    };

    private static ProductStock Stock(long total, long min, bool bought) => new()
    {
        MinStockMilli = min,
        LastPurchaseDate = bought ? new DateOnly(2026, 11, 1) : null,
        Locations = [new LocationQuantity { LocationId = Storage, QuantityMilli = total }]
    };
}
