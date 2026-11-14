using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

// D-60. A bottle of oil costs 1 USD; what changes from batch to batch is the purchase rate, so 1 USD at 200 = 200 CUP.
// Every expected number is worked out by hand in the comment next to it.

public class FifoBookTests
{
    private static readonly DateOnly Day = new(2026, 11, 14);

    private static long Bottles(long count) => count * 1_000;

    /// <summary>1 USD at that rate, per bottle.</summary>
    private static UnitCost AtRate(long rate) => new(1_000_000, rate * 1_000_000);

    private static FifoMovement Purchase(long bottles, long rate, Guid? lineId = null, int day = 0) =>
        new(Guid.NewGuid(), Day.AddDays(day), StockMovementKind.Purchase, Bottles(bottles), 1_000_000,
            rate * 1_000_000, lineId ?? Guid.NewGuid());

    private static FifoMovement Out(StockMovementKind kind, long bottles, int day = 0) =>
        new(Guid.NewGuid(), Day.AddDays(day), kind, -Bottles(bottles), null, null, null);

    private static FifoMovement In(StockMovementKind kind, long bottles, int day = 0) =>
        new(Guid.NewGuid(), Day.AddDays(day), kind, Bottles(bottles), null, null, null);

    [Fact]
    public void Take_ShouldEmptyTheOldestBatchFirst()
    {
        // 20 at 200, then 20 at 400. 25 sold: the 20 of 200 and 5 of 400 = 4,000 + 2,000 = 6,000 CUP.
        var second = Guid.NewGuid();
        var book = FifoBook.Replay([Purchase(20, 200), Purchase(20, 400, second, day: 15)]);

        var take = book.Take(Bottles(25), Day.AddDays(25));

        take.CupCents(book.NewestCost).Should().Be(600_000);
        take.UnitCost(book.NewestCost).Should().Be(new UnitCost(1_000_000, 240_000_000));   // 6,000 / 25 = 240
        book.StockMilli.Should().Be(Bottles(15));
        book.CurrentLayer!.PurchaseLineId.Should().Be(second);
        book.CurrentCost.Should().Be(AtRate(400));
    }

    [Fact]
    public void Replay_ShouldCloseTheBag_WhatSoldPlusWhatIsLeftIsWhatWasPaid()
    {
        // Paid: 20×200 + 30×220 + 30×260 = 4,000 + 6,600 + 7,800 = 18,400.
        // 50 sold = 20×200 + 30×220 = 10,600; left 30×260 = 7,800. 10,600 + 7,800 = 18,400.
        var book = FifoBook.Replay([
            Purchase(20, 200), Purchase(30, 220, day: 5), Purchase(30, 260, day: 18),
            Out(StockMovementKind.Sale, 30, day: 20), Out(StockMovementKind.Sale, 20, day: 21)
        ]);

        book.Takes.Sum(take => take.CupCents(book.NewestCost)).Should().Be(1_060_000);
        book.StockCupCents.Should().Be(780_000);
        book.Takes[0].CupCents(book.NewestCost).Should().Be(620_000);   // 20×200 + 10×220 = 4,000 + 2,200
        book.Takes[1].CupCents(book.NewestCost).Should().Be(440_000);   // 20×220
    }

    [Fact]
    public void Replay_ShouldIgnoreTransfers_BecauseBatchesBelongToTheProduct()
    {
        var book = FifoBook.Replay([
            Purchase(10, 200),
            Out(StockMovementKind.TransferOut, 4), In(StockMovementKind.TransferIn, 4)
        ]);

        book.StockMilli.Should().Be(Bottles(10));
        book.Layers.Should().ContainSingle().Which.RemainingMilli.Should().Be(Bottles(10));
        book.Takes.Should().BeEmpty();
    }

    [Fact]
    public void Replay_ShouldTakeExitsAndCountShortages_LikeSales()
    {
        // 10 at 200 and 10 at 300: a 3-bottle loss and a 9-bottle shortage take the 10 of 200 and 2 of 300.
        var book = FifoBook.Replay([
            Purchase(10, 200), Purchase(10, 300),
            Out(StockMovementKind.Exit, 3), Out(StockMovementKind.CountAdjustment, 9)
        ]);

        book.Takes[0].CupCents(book.NewestCost).Should().Be(60_000);    // 3×200
        book.Takes[1].CupCents(book.NewestCost).Should().Be(200_000);   // 7×200 + 2×300 = 2,000
        book.CurrentCost.Should().Be(AtRate(300));
    }

    [Fact]
    public void Replay_ShouldPutACountSurplus_AtTheNewestBatchCost()
    {
        // 10 at 200, then 10 at 300; 2 more found: they enter at 300, behind the others.
        var book = FifoBook.Replay([
            Purchase(10, 200), Purchase(10, 300), In(StockMovementKind.CountAdjustment, 2)
        ]);

        book.Layers.Should().HaveCount(3);
        book.Layers[2].Cost.Should().Be(AtRate(300));
        book.Layers[2].PurchaseLineId.Should().BeNull();
        book.StockCupCents.Should().Be(560_000);   // 2,000 + 3,000 + 600
    }

    [Fact]
    public void Replay_ShouldPayWhatWasSoldBeyondTheStock_WithTheNextPurchase()
    {
        // 5 at 200 on the shelf, 10 sold (the purchase was typed in later): 5 of 200 + 5 still pending.
        // Until the purchase comes, the pending ones are valued at the newest cost: 5×200 + 5×200 = 2,000.
        var book = new FifoBook();
        book.Apply(Purchase(5, 200));
        var take = book.Take(Bottles(10), Day);

        take.PendingMilli.Should().Be(Bottles(5));
        take.CupCents(book.NewestCost).Should().Be(200_000);
        book.StockMilli.Should().Be(Bottles(-5));

        // Then 10 arrive at 300: 5 of them were the ones sold. 5×200 + 5×300 = 2,500; 5 left at 300.
        book.Apply(Purchase(10, 300));

        take.PendingMilli.Should().Be(0);
        take.CupCents(book.NewestCost).Should().Be(250_000);
        book.StockMilli.Should().Be(Bottles(5));
        book.StockCupCents.Should().Be(150_000);
    }

    [Fact]
    public void Replay_ShouldCostWhatWasThereBeforeTheFirstPurchase_AtTheFirstPurchaseCost()
    {
        // 10 counted before ever buying, 4 of them sold, then 10 bought at 200: everything is worth 200.
        var book = FifoBook.Replay([
            In(StockMovementKind.CountAdjustment, 10), Out(StockMovementKind.Sale, 4), Purchase(10, 200)
        ]);

        book.Takes.Single().PendingMilli.Should().Be(0);
        book.Takes.Single().CupCents(book.NewestCost).Should().Be(80_000);   // 4×200
        book.StockCupCents.Should().Be(320_000);                            // 16×200
    }

    [Fact]
    public void CurrentCost_ShouldBeTheNewestBatch_WhenTheShelfIsEmpty()
    {
        var book = FifoBook.Replay([Purchase(10, 200), Purchase(10, 300), Out(StockMovementKind.Sale, 20)]);

        book.CurrentLayer.Should().BeNull();
        book.CurrentCost.Should().Be(AtRate(300));
    }

    [Fact]
    public void CurrentCost_ShouldBeNull_WhenNeverBought()
    {
        new FifoBook().CurrentCost.Should().BeNull();
    }
}

public class FifoCostingTests
{
    private static readonly DateOnly Day = new(2026, 11, 14);

    private static FifoBook TwoBatches()
    {
        // 10 bottles of 1 USD at 200, then 10 at 220.
        var book = new FifoBook();
        book.Put(10_000, new UnitCost(1_000_000, 200_000_000), Guid.NewGuid(), Day);
        book.Put(10_000, new UnitCost(1_000_000, 220_000_000), Guid.NewGuid(), Day);
        return book;
    }

    [Fact]
    public void Sale_ShouldAddTheColchon_ToTheRealCost()
    {
        // 12 sold: 10×200 + 2×220 = 2,440 CUP real. Colchón 20 CUP per USD × 12 USD = 240. A2 = 2,680.
        var cost = FifoCosting.Sale(TwoBatches(), 12_000, 2_000, Day);

        cost.RealCents.Should().Be(244_000);
        cost.BufferCents.Should().Be(24_000);
        cost.CostCents.Should().Be(268_000);
        cost.UnitCost.Should().Be(new UnitCost(1_000_000, 203_333_333));   // 2,440 / 12
    }

    [Fact]
    public void Sale_ShouldMoveTheBook_SoTheNextLineTakesWhatIsLeft()
    {
        // Two counters of the same product: 8 then 8. The second takes 2 of 200 and 6 of 220 = 400 + 1,320.
        var book = TwoBatches();
        FifoCosting.Sale(book, 8_000, 0, Day);

        FifoCosting.Sale(book, 8_000, 0, Day).RealCents.Should().Be(172_000);
    }

    [Fact]
    public void Sale_ShouldGiveBackANegativeSale_AtTheNewestCost()
    {
        // 2 more on the counter than expected: A2 goes down 2×220 = 440, and 2×20 = 40 of colchón.
        var book = TwoBatches();

        var cost = FifoCosting.Sale(book, -2_000, 2_000, Day);

        cost.RealCents.Should().Be(-44_000);
        cost.BufferCents.Should().Be(-4_000);
        book.StockMilli.Should().Be(22_000);
    }

    [Fact]
    public void Sale_ShouldCostNothing_WhenNeverBought()
    {
        var cost = FifoCosting.Sale(new FifoBook(), 3_000, 2_000, Day);

        cost.CostCents.Should().Be(0);
        cost.UnitCost.Should().BeNull();
    }

    [Fact]
    public void WithBuffer_ShouldPriceTheUnit_AtThePurchaseRatePlusTheColchon()
    {
        // 0.62 USD at 325 = 201.50 CUP; + 0.62 × 20 = 12.40 → 213.90.
        FifoCosting.WithBuffer(new UnitCost(620_000, 201_500_000), 2_000)
            .Should().Be(new UnitCost(620_000, 213_900_000));
    }
}
