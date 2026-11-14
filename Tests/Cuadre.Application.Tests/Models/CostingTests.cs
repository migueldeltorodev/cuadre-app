using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

// Every expected number is worked out by hand in the comment next to it.

public class PurchaseCalculatorTests
{
    private static readonly Guid SomeBox = Guid.NewGuid();

    private static Purchase NewPurchase(Currency currency, long rateCents = 32_500) => new()
    {
        Id = Guid.NewGuid(),
        Date = new DateOnly(2026, 11, 12),
        SupplierId = Guid.NewGuid(),
        Currency = currency,
        RateCupPerUsdCents = rateCents,
        CashBoxId = SomeBox
    };

    private static PurchaseLine Line(long quantityMilli, long unitCostCents, long factorMilli) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        QuantityMilli = quantityMilli,
        UnitCostCents = unitCostCents,
        ConversionFactorMilli = factorMilli
    };

    private static PurchaseCharge Charge(Currency currency, long amountCents) => new()
    {
        Id = Guid.NewGuid(),
        Description = "Flete",
        Currency = currency,
        AmountCents = amountCents,
        CashBoxId = SomeBox
    };

    [Fact]
    public void StockQuantityMilli_ShouldMultiplyByTheFactor()
    {
        // 2.5 sacks × 100 lb = 250 lb
        PurchaseCalculator.StockQuantityMilli(2_500, 100_000).Should().Be(250_000);
    }

    [Fact]
    public void StockQuantityMilli_ShouldBeNull_WhenTheResultIsNotAnExactThousandth()
    {
        // 0.001 × 1.5 = 0.0015
        PurchaseCalculator.StockQuantityMilli(1, 1_500).Should().BeNull();
    }

    [Fact]
    public void ToUsd_ShouldConvertCupAtTheRate_AndKeepUsd()
    {
        PurchaseCalculator.ToUsd(32_500_00, Currency.CUP, 32_500).Should().Be(100m);
        PurchaseCalculator.ToUsd(1_250, Currency.USD, 32_500).Should().Be(12.5m);
    }

    [Fact]
    public void Calculate_ShouldGiveLandedCostPerStockUnit_WithoutCharges()
    {
        // 2 sacks of 100 lb at 62 USD → 124 USD / 200 lb = 0.62 USD/lb; × 325 = 201.50 CUP/lb
        var purchase = NewPurchase(Currency.USD);
        purchase.Lines.Add(Line(2_000, 6_200, 100_000));

        PurchaseCalculator.Calculate(purchase);

        var line = purchase.Lines[0];
        line.StockQuantityMilli.Should().Be(200_000);
        line.LineTotalCents.Should().Be(12_400);
        line.AllocatedChargesUsdMicros.Should().Be(0);
        line.LandedUnitCostUsdMicros.Should().Be(620_000);
        line.LandedUnitCostCupMicros.Should().Be(201_500_000);
        purchase.GoodsTotalCents.Should().Be(12_400);
    }

    [Fact]
    public void Calculate_ShouldSpreadChargesByValue()
    {
        // Rice 100 USD + oil 300 USD; 40 USD freight → rice carries 10, oil 30.
        // Rice: 110 USD / 100 lb = 1.10. Oil: 330 USD / 36 units = 9.1666… → 9.166667
        var purchase = NewPurchase(Currency.USD);
        purchase.Lines.Add(Line(1_000, 10_000, 100_000));
        purchase.Lines.Add(Line(3_000, 10_000, 12_000));
        purchase.Charges.Add(Charge(Currency.USD, 4_000));

        PurchaseCalculator.Calculate(purchase);

        purchase.Lines[0].AllocatedChargesUsdMicros.Should().Be(10_000_000);
        purchase.Lines[1].AllocatedChargesUsdMicros.Should().Be(30_000_000);
        purchase.Lines[0].LandedUnitCostUsdMicros.Should().Be(1_100_000);
        purchase.Lines[1].LandedUnitCostUsdMicros.Should().Be(9_166_667);
        purchase.GoodsTotalCents.Should().Be(40_000);   // charges are NOT part of the goods total
    }

    [Fact]
    public void Calculate_ShouldConvertCupGoodsAndCharges_AtThePurchaseRate()
    {
        // 32,500 CUP of rice (= 100 USD) + 3,250 CUP freight (= 10 USD) → 110 USD / 100 lb = 1.10 USD/lb
        // CUP: 1.10 × 325 = 357.50
        var purchase = NewPurchase(Currency.CUP);
        purchase.Lines.Add(Line(1_000, 32_500_00, 100_000));
        purchase.Charges.Add(Charge(Currency.CUP, 3_250_00));

        PurchaseCalculator.Calculate(purchase);

        purchase.Lines[0].LandedUnitCostUsdMicros.Should().Be(1_100_000);
        purchase.Lines[0].LandedUnitCostCupMicros.Should().Be(357_500_000);
    }

    [Fact]
    public void Calculate_ShouldMixCurrencies_WhenAChargeIsInAnotherCurrency()
    {
        // USD purchase, freight paid in CUP: 1,625 CUP / 325 = 5 USD → (100 + 5) / 100 lb = 1.05
        var purchase = NewPurchase(Currency.USD);
        purchase.Lines.Add(Line(1_000, 10_000, 100_000));
        purchase.Charges.Add(Charge(Currency.CUP, 1_625_00));

        PurchaseCalculator.Calculate(purchase);

        purchase.Lines[0].LandedUnitCostUsdMicros.Should().Be(1_050_000);
    }
}

public class CostCalculatorTests
{
    private static CostMovement In(long quantityMilli, long usdMicros, long rateCents = 32_500) =>
        new(quantityMilli, usdMicros, usdMicros * rateCents / 100);

    private static CostMovement Out(long quantityMilli) => new(-quantityMilli, null, null);

    [Fact]
    public void Replay_ShouldBeNull_WithoutCostedEntries()
    {
        CostCalculator.Replay([]).Should().BeNull();
        CostCalculator.Replay([Out(10_000)]).Should().BeNull();
    }

    [Fact]
    public void Replay_ShouldWeightByQuantity()
    {
        // 100 lb at 0.60 + 100 lb at 0.70 → 0.65; CUP at 325: 195 and 227.5 → 211.25
        var cost = CostCalculator.Replay([In(100_000, 600_000), In(100_000, 700_000)]);

        cost.Should().Be(new UnitCost(650_000, 211_250_000));
    }

    [Fact]
    public void Replay_ShouldUseTheStockExistingAtThatMoment()
    {
        // 100 at 0.60, sell 50, 50 at 0.90 → (50 × 0.60 + 50 × 0.90) / 100 = 0.75
        var cost = CostCalculator.Replay([In(100_000, 600_000), Out(50_000), In(50_000, 900_000)]);

        cost!.UsdMicros.Should().Be(750_000);
    }

    [Fact]
    public void Replay_ShouldRestartTheAverage_WhenStockIsZeroOrNegative()
    {
        // Sold 150 having bought 100 (stock −50): the next purchase sets the cost on its own.
        var cost = CostCalculator.Replay([In(100_000, 600_000), Out(150_000), In(100_000, 900_000)]);

        cost!.UsdMicros.Should().Be(900_000);
    }

    [Fact]
    public void Replay_ShouldKeepTheHistoricalCup_OfEachPurchaseRate()
    {
        // Same USD cost, rates 300 and 350 → CUP 180 and 210 → average 195 (RN-02)
        var cost = CostCalculator.Replay([In(100_000, 600_000, 30_000), In(100_000, 600_000, 35_000)]);

        cost.Should().Be(new UnitCost(600_000, 195_000_000));
    }
}

public class PriceCalculatorTests
{
    // 0.62 USD/lb bought at 325 → 201.50 CUP/lb
    private static readonly UnitCost Rice = new(620_000, 201_500_000);

    [Fact]
    public void PurchaseRateCents_ShouldBeCupCostOverUsdCost()
    {
        PriceCalculator.PurchaseRateCents(Rice).Should().Be(32_500);
    }

    [Fact]
    public void SuggestedPriceCents_ShouldMatchTheDesignExample()
    {
        // (201.50 + 0.62 × 30) × 1.20 = 220.10 × 1.20 = 264.12 → step 5 → 265
        PriceCalculator.SuggestedPriceCents(Rice, bufferCents: 3_000, markupBps: 2_000, roundingStepCents: 500)
            .Should().Be(26_500);
    }

    [Fact]
    public void SuggestedPriceCents_ShouldNotJump_WhenAlreadyOnAStep()
    {
        var cost = new UnitCost(615_385, 200_000_000);   // exactly 200.00 CUP

        PriceCalculator.SuggestedPriceCents(cost, 0, 0, 100).Should().Be(20_000);
    }

    [Fact]
    public void SuggestedPriceCents_ShouldRoundUp_NeverDown()
    {
        // 201.50 → whole pesos → 202
        PriceCalculator.SuggestedPriceCents(Rice, 0, 0, 100).Should().Be(20_200);
    }

    [Fact]
    public void MarginBps_ShouldBeOverThePrice()
    {
        // (265 − 201.50) / 265 = 23.96 %
        PriceCalculator.MarginBps(26_500, Rice).Should().Be(2_396);
        PriceCalculator.MarginBps(null, Rice).Should().BeNull();
    }

    [Theory]
    [InlineData(34_000L, true)]    // 340 > 325 + 10
    [InlineData(33_500L, false)]   // exactly on the limit: still covered
    [InlineData(null, false)]      // no rate loaded: no alert
    public void IsBufferExceeded_ShouldCompareTodayWithRatePlusBuffer(long? today, bool expected)
    {
        PriceCalculator.IsBufferExceeded(today, 32_500, 1_000).Should().Be(expected);
    }
}

public class ProductPricingRulesTests
{
    private static readonly BusinessSettings Settings = new()
    {
        Name = "Test",
        ExchangeBufferCents = 3_000,
        DefaultMarkupBps = 2_000,
        PriceRoundingStepCents = 500
    };

    private static Product Rice(long? ownBufferCents = null, int? ownMarkupBps = null, bool bought = true,
        long? lastUsdMicros = null, long? lastCupMicros = null) => new()
    {
        Id = Guid.NewGuid(),
        Code = "ARR-001",
        Name = "Arroz",
        CategoryId = Guid.NewGuid(),
        PurchaseUnit = MeasureUnit.Sack,
        StockUnit = MeasureUnit.Pound,
        ConversionFactorMilli = 100_000,
        SalePriceCents = 25_000,
        ExchangeBufferCents = ownBufferCents,
        MarkupBps = ownMarkupBps,
        AvgCostUsdMicros = bought ? 620_000 : null,
        AvgCostCupMicros = bought ? 201_500_000 : null,
        LastCostUsdMicros = bought ? lastUsdMicros ?? 620_000 : null,
        LastCostCupMicros = bought ? lastCupMicros ?? 201_500_000 : null
    };

    [Fact]
    public void Build_ShouldBeNull_WhenTheProductWasNeverBought()
    {
        ProductPricingRules.Build(Rice(bought: false), Settings, 33_000).Should().BeNull();
    }

    [Fact]
    public void Build_ShouldUseTheBusinessDefaults_WhenTheProductHasNoOwnValues()
    {
        var pricing = ProductPricingRules.Build(Rice(), Settings, 36_000)!;

        pricing.EffectiveBufferCents.Should().Be(3_000);
        pricing.EffectiveMarkupBps.Should().Be(2_000);
        pricing.SuggestedPriceCents.Should().Be(26_500);
        pricing.IsBufferExceeded.Should().BeTrue();     // 360 > 325 + 30
    }

    [Fact]
    public void Build_ShouldPreferTheProductsOwnBufferAndMarkup()
    {
        // (201.50 + 0) × 1.00 = 201.50 → step 5 → 205
        var pricing = ProductPricingRules.Build(Rice(ownBufferCents: 0, ownMarkupBps: 0), Settings, null)!;

        pricing.SuggestedPriceCents.Should().Be(20_500);
        pricing.TodayRateCents.Should().BeNull();
        pricing.IsBufferExceeded.Should().BeFalse();
    }

    [Fact]
    public void Build_ShouldPriceFromTheLastCost_AndKeepTheAverageBeside()
    {
        // D-52: average 0.62 at 325; last purchase 0.70 at 350 = 245 CUP.
        // (245 + 0.70 x 30) x 1.20 = 266 x 1.20 = 319.20 -> step 5 -> 320. Margin at 250 against 245 = 2 %.
        var pricing = ProductPricingRules.Build(Rice(lastUsdMicros: 700_000, lastCupMicros: 245_000_000), Settings, null)!;

        pricing.AvgCostCupMicros.Should().Be(201_500_000);
        pricing.LastCostCupMicros.Should().Be(245_000_000);
        pricing.LastRateCents.Should().Be(35_000);
        pricing.SuggestedPriceCents.Should().Be(32_000);
        pricing.MarginBps.Should().Be(200);
    }

    [Theory]
    [InlineData(38_000L, false)]   // exactly 350 + 30: still covered
    [InlineData(38_100L, true)]    // beyond 350 + 30
    [InlineData(35_600L, false)]   // beyond the AVERAGE rate + 30 (355), but not the last purchase's
    public void Build_ShouldCompareTodaysRate_WithTheLastPurchaseRate(long today, bool expected)
    {
        var pricing = ProductPricingRules.Build(Rice(lastUsdMicros: 700_000, lastCupMicros: 245_000_000), Settings, today)!;

        pricing.IsBufferExceeded.Should().Be(expected);
    }

    [Fact]
    public void Build_ShouldFallBackToTheAverage_WhenThereIsNoLastCost()
    {
        var withoutLast = new Product
        {
            Id = Guid.NewGuid(),
            Code = "ARR-001",
            Name = "Arroz",
            CategoryId = Guid.NewGuid(),
            PurchaseUnit = MeasureUnit.Sack,
            StockUnit = MeasureUnit.Pound,
            ConversionFactorMilli = 100_000,
            SalePriceCents = 25_000,
            AvgCostUsdMicros = 620_000,
            AvgCostCupMicros = 201_500_000
        };

        var pricing = ProductPricingRules.Build(withoutLast, Settings, null)!;

        pricing.LastCostCupMicros.Should().Be(201_500_000);
        pricing.SuggestedPriceCents.Should().Be(26_500);
    }
}
