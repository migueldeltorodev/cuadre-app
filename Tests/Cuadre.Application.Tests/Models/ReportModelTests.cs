using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

// Slice 8: the pure rules of the reports. 130 CUP the lb = 130,000,000 micros.

public class ReportModelTests
{
    private const long CostPerLb = 130_000_000;
    private static readonly DateOnly Day = new(2026, 11, 12);

    private static StockLossRow Exit(ExitReason reason, long pounds, long? cost = CostPerLb) => new()
    {
        Date = Day,
        Kind = StockOperationKind.Exit,
        ExitReason = reason,
        QuantityMilli = pounds * 1_000,
        UnitCostCupMicros = cost
    };

    private static StockLossRow Count(long difference) => new()
    {
        Date = Day,
        Kind = StockOperationKind.Count,
        QuantityMilli = difference * 1_000,
        UnitCostCupMicros = CostPerLb
    };

    // ---------- Losses (RF-REP-08) ----------

    [Theory]
    [InlineData(ExitReason.Shrinkage, LossKind.Shrinkage)]
    [InlineData(ExitReason.Expired, LossKind.Expired)]
    [InlineData(ExitReason.Damaged, LossKind.Damaged)]
    public void Classify_ShouldTakeTheExitsThatAreLosses(ExitReason reason, LossKind expected)
    {
        var (losses, gain) = LossReport.Classify([Exit(reason, 2)]);

        var loss = losses.Should().ContainSingle().Subject;
        loss.Kind.Should().Be(expected);
        loss.QuantityMilli.Should().Be(2_000);
        loss.CostCents.Should().Be(26_000);
        gain.Should().Be(0);
    }

    [Theory]
    [InlineData(ExitReason.HouseConsumption)]
    [InlineData(ExitReason.Adjustment)]
    public void Classify_ShouldLeaveOutHouseConsumptionAndAdjustments(ExitReason reason)
    {
        var (losses, _) = LossReport.Classify([Exit(reason, 2)]);

        losses.Should().BeEmpty();
    }

    [Fact]
    public void Classify_ShouldTurnAMissingCountIntoAPositiveLoss_AndAddUpTheGains()
    {
        var (losses, gain) = LossReport.Classify([Count(-7), Count(3)]);

        var loss = losses.Should().ContainSingle().Subject;
        loss.Kind.Should().Be(LossKind.CountDifference);
        loss.QuantityMilli.Should().Be(7_000);
        loss.CostCents.Should().Be(91_000);
        gain.Should().Be(39_000);
    }

    [Fact]
    public void Classify_ShouldValueAtZero_WhenTheProductWasNeverBought()
    {
        var (losses, _) = LossReport.Classify([Exit(ExitReason.Damaged, 2, cost: null)]);

        losses.Single().CostCents.Should().Be(0);
    }

    [Fact]
    public void CostCents_ShouldRoundLikeTheCostOfSales()
    {
        // 0.333 lb × 130 CUP = 43.29 CUP.
        LossReport.CostCents(333, CostPerLb).Should().Be(4_329);
        // 0.001 lb × 0.5 CUP = 0.0005 CUP → 0.05 cents → 0.
        LossReport.CostCents(1, 500_000).Should().Be(0);
    }

    // ---------- Suppliers (RF-REP-03) ----------

    private static PurchasePoint Bought(Guid supplierId, int number, DateOnly date, long pounds, long usdMicros) => new()
    {
        PurchaseNumber = number,
        Date = date,
        SupplierId = supplierId,
        SupplierName = supplierId.ToString()[..4],
        StockQuantityMilli = pounds * 1_000,
        LandedUnitCostUsdMicros = usdMicros,
        LandedUnitCostCupMicros = usdMicros * 325
    };

    [Fact]
    public void Summarize_ShouldWeighTheAverageByQuantity_AndPutTheCheapestFirst()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var suppliers = SupplierCost.Summarize([
            Bought(a, 1, Day.AddDays(-10), 300, 400_000),
            Bought(a, 3, Day, 100, 440_000),
            Bought(b, 2, Day.AddDays(-5), 100, 420_000)
        ]);

        suppliers.Select(x => x.SupplierId).Should().Equal(a, b);
        var first = suppliers[0];
        first.PurchaseCount.Should().Be(2);
        first.AverageUnitCostUsdMicros.Should().Be(410_000);   // (300 × 400 + 100 × 440) / 400
        first.LastDate.Should().Be(Day);
        first.LastUnitCostUsdMicros.Should().Be(440_000);
        first.MinUnitCostUsdMicros.Should().Be(400_000);
        first.MaxUnitCostUsdMicros.Should().Be(440_000);
        first.TotalQuantityMilli.Should().Be(400_000);
    }

    // ---------- Cost changes (RF-REP-02) ----------

    [Fact]
    public void CostChanges_ShouldCompareTheFirstAndTheLastPurchase_BiggestRiseFirst()
    {
        var rice = Guid.NewGuid();
        var beans = Guid.NewGuid();
        PurchasePoint Of(Guid product, int number, int daysAgo, long usdMicros) => new()
        {
            ProductId = product,
            ProductName = product == rice ? "Arroz" : "Frijol",
            PurchaseNumber = number,
            Date = Day.AddDays(-daysAgo),
            LandedUnitCostUsdMicros = usdMicros,
            LandedUnitCostCupMicros = usdMicros * 325
        };

        var changes = ProductCostChange.Summarize([
            Of(rice, 2, 5, 420_000),
            Of(rice, 1, 10, 400_000),
            Of(rice, 3, 0, 440_000),
            Of(beans, 4, 3, 1_000_000),
            Of(beans, 5, 1, 1_250_000)
        ]);

        changes.Select(x => x.ProductId).Should().Equal(beans, rice);
        changes[0].UsdChangeBps.Should().Be(2_500);
        var riceChange = changes[1];
        riceChange.PurchaseCount.Should().Be(3);
        riceChange.FirstDate.Should().Be(Day.AddDays(-10));
        riceChange.FirstUnitCostUsdMicros.Should().Be(400_000);
        riceChange.LastUnitCostUsdMicros.Should().Be(440_000);
        riceChange.UsdChangeBps.Should().Be(1_000);
        riceChange.CupChangeBps.Should().Be(1_000);
    }

    // ---------- Product ranking (RF-REP-04) ----------

    [Fact]
    public void Ranking_ShouldComputeTheMargin_AndLeaveItNullWithoutSales()
    {
        var sold = new ProductRanking(Guid.NewGuid(), "ARR-001", "Arroz", MeasureUnit.Pound, 100_000, 2_500_000,
            1_300_000, 0, 0);
        var onlyLost = new ProductRanking(Guid.NewGuid(), "FRI-001", "Frijol", MeasureUnit.Pound, 0, 0, 0, 5_000,
            65_000);

        sold.MarginCents.Should().Be(1_200_000);
        sold.MarginBps.Should().Be(4_800);
        onlyLost.MarginBps.Should().BeNull();
    }
}
