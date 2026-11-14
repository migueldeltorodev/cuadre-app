using Cuadre.Application.Models;
using FluentAssertions;

namespace Cuadre.Application.Tests.Models;

// D-61. Colchón 0, recargo 0, step 1 CUP: the suggested price is the CUP cost rounded up to the peso.
// Rice at 325: 0.60 USD/lb = 195 CUP (batch on sale), 0.70 USD/lb = 227.50 CUP (newest batch) → 228.

public class PriceNoticeTests
{
    private static readonly BusinessSettings Settings = new()
    {
        Name = "Test",
        ExchangeBufferCents = 0,
        DefaultMarkupBps = 0,
        PriceRoundingStepCents = 100
    };

    private static readonly Guid First = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();

    private static Product Rice(long? priceCents, Guid? current, Guid? newest, Guid? reviewed = null,
        Guid? seen = null) => new()
    {
        Id = Guid.NewGuid(),
        Code = "ARR-001",
        Name = "Arroz",
        CategoryId = Guid.NewGuid(),
        PurchaseUnit = MeasureUnit.Sack,
        StockUnit = MeasureUnit.Pound,
        ConversionFactorMilli = 100_000,
        SalePriceCents = priceCents,
        AvgCostUsdMicros = 650_000,
        AvgCostCupMicros = 211_250_000,
        LastCostUsdMicros = 700_000,
        LastCostCupMicros = 227_500_000,
        CurrentCostUsdMicros = 600_000,
        CurrentCostCupMicros = 195_000_000,
        CurrentPurchaseLineId = current,
        LastPurchaseLineId = newest,
        PriceReviewedLineId = reviewed,
        CostlierSeenLineId = seen
    };

    private static List<PriceNotice> NoticesOf(Product product) =>
        PriceNotices.For(product, ProductPricingRules.Build(product, Settings, null)!).ToList();

    [Fact]
    public void Build_ShouldPriceFromTheBatchBeingSold_AndKeepTheNewestBeside()
    {
        var pricing = ProductPricingRules.Build(Rice(25_000, First, Second), Settings, null)!;

        pricing.SuggestedPriceCents.Should().Be(19_500);
        pricing.NewestSuggestedPriceCents.Should().Be(22_800);
        pricing.CurrentRateCents.Should().Be(32_500);
        pricing.MarginBps.Should().Be(2_200);   // (250 − 195) / 250
    }

    [Fact]
    public void For_ShouldTellABatchChanged_WhenThePriceWasDecidedForAnotherBatch()
    {
        var notice = NoticesOf(Rice(20_000, Second, Second, reviewed: First)).Should().ContainSingle().Subject;

        notice.Kind.Should().Be(PriceNoticeKind.BatchChanged);
        notice.SuggestedPriceCents.Should().Be(19_500);
    }

    [Fact]
    public void For_ShouldStayQuiet_WhenThePriceWasDecidedForTheBatchOnSale()
    {
        NoticesOf(Rice(25_000, First, First, reviewed: First)).Should().BeEmpty();
    }

    [Fact]
    public void For_ShouldStayQuiet_WhenTheNewBatchAlreadyHasItsSuggestedPrice()
    {
        NoticesOf(Rice(19_500, Second, Second, reviewed: First)).Should().BeEmpty();
    }

    [Fact]
    public void For_ShouldWarnEarly_WhenTheWaitingBatchWillNotFitInThePrice()
    {
        var notice = NoticesOf(Rice(20_000, First, Second, reviewed: First)).Should().ContainSingle().Subject;

        notice.Kind.Should().Be(PriceNoticeKind.NewBatchCostlier);
        notice.SuggestedPriceCents.Should().Be(22_800);
        notice.BatchCostCupMicros.Should().Be(227_500_000);
    }

    [Theory]
    [InlineData(22_800L)]   // the price already covers the waiting batch
    [InlineData(30_000L)]
    public void For_ShouldNotWarn_WhenThePriceCoversTheWaitingBatch(long priceCents)
    {
        NoticesOf(Rice(priceCents, First, Second, reviewed: First)).Should().BeEmpty();
    }

    [Fact]
    public void For_ShouldNotWarnAgain_AfterTheWarningOfThatBatchWasDismissed()
    {
        NoticesOf(Rice(20_000, First, Second, reviewed: First, seen: Second)).Should().BeEmpty();
    }

    [Fact]
    public void For_ShouldStayQuiet_WithAnEmptyShelf()
    {
        NoticesOf(Rice(20_000, current: null, newest: Second)).Should().BeEmpty();
    }
}
