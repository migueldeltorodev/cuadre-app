using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

// Every expected number is worked out by hand in the comment next to it.
// Money in CUP cents, quantities in thousandths, unit costs in millionths of a peso.

public class ClosingMathTests
{
    private const long CostOf150Pesos = 150_000_000;   // 150 CUP in micros

    // ---------- Count (RN-05, RN-06, RN-07) ----------

    [Fact]
    public void Count_ShouldComputeSoldSalesToleranceAndCost()
    {
        // expected = 10 + 5 − 2 = 13; sold = 13 − 8 = 5
        // sales = 5 × 250 = 1,250 CUP; tolerance 2% = 25 CUP; cost = 5 × 150 = 750 CUP
        var result = ClosingCalculator.Count(new CountInput(10_000, 5_000, 2_000, 8_000, 25_000, 200, CostOf150Pesos));

        result.Should().Be(new CountResult(13_000, 5_000, 125_000, 2_500, 75_000));
    }

    [Fact]
    public void Count_ShouldLowerSalesAndCostWithoutTolerance_WhenMoreWasFoundThanExpected()
    {
        // expected 10, counted 11 → sold −1: −250 CUP of sales, −150 CUP of cost, no shrinkage
        var result = ClosingCalculator.Count(new CountInput(10_000, 0, 0, 11_000, 25_000, 200, CostOf150Pesos));

        result.Should().Be(new CountResult(10_000, -1_000, -25_000, 0, -15_000));
    }

    [Fact]
    public void Count_ShouldRoundTheSalesAwayFromZero_WhenSoldInBulk()
    {
        // 2.5 lb × 123.45 CUP = 308.625 CUP → 30,862.5 cents → 30,863
        var result = ClosingCalculator.Count(new CountInput(10_000, 0, 0, 7_500, 12_345, 0, null));

        result.SalesCents.Should().Be(30_863);
        result.CostCents.Should().Be(0);
    }

    [Fact]
    public void Count_ShouldBeZero_WhenNothingWasSoldAndThereIsNoPrice()
    {
        var result = ClosingCalculator.Count(new CountInput(4_000, 0, 0, 4_000, null, 200, null));

        result.Should().Be(new CountResult(4_000, 0, 0, 0, 0));
    }

    // ---------- Difference (RN-06c) ----------

    [Theory]
    [InlineData(-3_000L, 2_500L, -500L)]   // the shrinkage covers 25 of the 30 CUP missing
    [InlineData(-2_000L, 2_500L, 0L)]      // fully covered
    [InlineData(1_500L, 2_500L, 1_500L)]   // a surplus is kept as it is
    [InlineData(0L, 0L, 0L)]
    public void AdjustedDifference_ShouldCoverAShortageWithTheTolerance(long difference, long tolerance,
        long expected)
    {
        ClosingCalculator.AdjustedDifference(difference, tolerance).Should().Be(expected);
    }

    // ---------- Staff pay (RN-08) ----------

    [Theory]
    [InlineData(100_000L, 100, 5_000_000L, 150_000L)]   // 1,000 + 1% of 50,000 = 1,500 CUP
    [InlineData(100_000L, 100, 1_234_567L, 112_300L)]   // 1,000 + 123.4567 = 1,123.4567 → 1,123 CUP
    [InlineData(100_000L, 100, 1_235_000L, 112_400L)]   // 1,000 + 123.50 = 1,123.50 → 1,124 CUP
    [InlineData(100_000L, 100, -50_000L, 100_000L)]     // nothing collected: the fixed part only
    [InlineData(0L, 0, 5_000_000L, 0L)]
    public void StaffPay_ShouldAddFixedAndPercentRoundedToWholePesos(long fixedCents, int bps, long collected,
        long expected)
    {
        ClosingCalculator.StaffPay(fixedCents, bps, collected).Should().Be(expected);
    }

    // ---------- Totals ----------

    [Fact]
    public void Totals_ShouldAddTheLinesAndComputeA1A3TheDifferenceAndThePay()
    {
        var lines = new[]
        {
            new CountResult(13_000, 5_000, 125_000, 2_500, 75_000),
            new CountResult(10_000, 2_500, 30_863, 0, 20_000)
        };

        // theoretical 1,558.63; collected 1,000 + 540 = 1,540 → difference −18.63, covered by 25 of shrinkage
        // pay = 1,000 + 1% of 1,540 = 1,015.40 → 1,015 CUP
        var totals = ClosingCalculator.Totals(lines, 100_000, 54_000, 100_000, 100);

        totals.Should().Be(new ClosingTotals(155_863, 2_500, 100_000, 54_000, 95_000, -1_863, 0, 101_500));
        totals.CollectedCents.Should().Be(154_000);
        totals.RealIncomeCents.Should().Be(59_000);
    }

    // ---------- Split (RN-10) ----------

    [Fact]
    public void Split_ShouldGiveA3ToTheOthers_WhenTheTransfersDoNotReachA2()
    {
        // The design example: A1 = 50,000 (10,000 by transfer), A2 = 20,000 → A3 = 30,000.
        // Reposición: 10,000 bank + 10,000 cash. The others: 30,000. No debt.
        var split = ClosingCalculator.Split(Totals(cash: 4_000_000, transfer: 1_000_000, cost: 2_000_000), 0);

        split.Should().Be(new ClosingSplit(3_000_000, 0, 0, 0));
    }

    [Fact]
    public void Split_ShouldCreateDebt_WhenTheTransfersGoBeyondA2()
    {
        // A1 = 10,000 all by transfer, A2 = 4,000 → A3 = 6,000 trapped in the bank, in Reposición.
        // The others get no cash today: Reposición owes them 6,000.
        var split = ClosingCalculator.Split(Totals(cash: 0, transfer: 1_000_000, cost: 400_000), 0);

        split.Should().Be(new ClosingSplit(0, 600_000, 0, 600_000));
    }

    [Fact]
    public void Split_ShouldRepayTheDebtWithTheCashReposicionWouldTake()
    {
        // A1 = 10,000 cash, A2 = 4,000 → A3 = 6,000. Debt 6,000: Reposición's 4,000 of cash go to the others.
        // others = 6,000 + 4,000 = 10,000; debt left = 2,000
        var split = ClosingCalculator.Split(Totals(cash: 1_000_000, transfer: 0, cost: 400_000), 600_000);

        split.Should().Be(new ClosingSplit(1_000_000, 0, 400_000, 200_000));
    }

    [Fact]
    public void Split_ShouldNotRepay_WhenA3IsNegative()
    {
        // A1 = 3,000, A2 = 4,000 → A3 = −1,000: the others give 1,000 back; the debt stays.
        var split = ClosingCalculator.Split(Totals(cash: 300_000, transfer: 0, cost: 400_000), 100_000);

        split.Should().Be(new ClosingSplit(-100_000, 0, 0, 100_000));
    }

    // ---------- Apportion ----------

    [Fact]
    public void Apportion_ShouldSplitByThePercents()
    {
        ClosingCalculator.Apportion(1_000, [5_000, 3_000, 2_000]).Should().Equal(500, 300, 200);
    }

    [Fact]
    public void Apportion_ShouldGiveTheLeftoverToTheLargestRemainder()
    {
        // 33.33 + 33.33 + 33.34 → 33 + 33 + 34
        ClosingCalculator.Apportion(100, [3_333, 3_333, 3_334]).Should().Equal(33, 33, 34);
    }

    [Fact]
    public void Apportion_ShouldBreakTiesToTheEarlierItem()
    {
        ClosingCalculator.Apportion(100, [1, 1, 1]).Should().Equal(34, 33, 33);
    }

    [Fact]
    public void Apportion_ShouldKeepTheSign_WhenTheAmountIsNegative()
    {
        ClosingCalculator.Apportion(-100, [1, 1, 1]).Should().Equal(-34, -33, -33);
    }

    [Fact]
    public void Apportion_ShouldBeEmpty_WhenThereAreNoPercents()
    {
        ClosingCalculator.Apportion(100, []).Should().BeEmpty();
    }

    // ---------- AssignBoxes ----------

    [Fact]
    public void AssignBoxes_ShouldTakeFromThePrimaryBoxFirst_AndSendNegativesToIt()
    {
        var primary = Guid.NewGuid();
        var other = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var e2 = Guid.NewGuid();
        var e3 = Guid.NewGuid();
        var e4 = Guid.NewGuid();

        // primary 500, other 300. E1 600 = 500 primary + 100 other; E2 150 from other; E3 −50 back to primary.
        var result = ClosingCalculator.AssignBoxes(
            [(other, 300), (primary, 500)], primary, [(e1, 600), (e2, 150), (e3, -50), (e4, 0)]);

        result.Should().BeEquivalentTo(new[]
        {
            new BoxShare(primary, e1, 500),
            new BoxShare(other, e1, 100),
            new BoxShare(other, e2, 150),
            new BoxShare(primary, e3, -50)
        });
    }

    [Fact]
    public void AssignBoxes_ShouldTakeTheRestFromThePrimaryBox_WhenTheCashIsNotEnough()
    {
        var primary = Guid.NewGuid();
        var envelope = Guid.NewGuid();

        var result = ClosingCalculator.AssignBoxes([(primary, 100)], primary, [(envelope, 150)]);

        result.Should().Equal(new BoxShare(primary, envelope, 150));
    }

    private static ClosingTotals Totals(long cash, long transfer, long cost) =>
        new(0, 0, cash, transfer, cost, 0, 0, 0);
}
