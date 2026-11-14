namespace Cuadre.Application.Models;

// Pure math of slice 3: no database, no clock. All of it is unit tested.
// Intermediate values are `decimal` (exact base 10, 28 digits); stored values are integers:
// money in cents, quantities in thousandths (Milli), unit costs in millionths (Micros).

public static class Units
{
    public const long MilliPerUnit = 1_000;
    public const long MicrosPerUnit = 1_000_000;

    public static long RoundToLong(decimal value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);
}

/// <summary>Turns purchase lines into stock quantities and LANDED costs (goods + share of the charges).</summary>
public static class PurchaseCalculator
{
    /// <summary>Purchase units × factor, in stock-unit thousandths. Null when it is not an exact thousandth.</summary>
    public static long? StockQuantityMilli(long quantityMilli, long conversionFactorMilli)
    {
        var product = (decimal)quantityMilli * conversionFactorMilli;
        return product % Units.MilliPerUnit == 0 ? (long)(product / Units.MilliPerUnit) : null;
    }

    /// <summary>CUP cents → USD (units) at the purchase rate; USD cents → USD.</summary>
    public static decimal ToUsd(long amountCents, Currency currency, long rateCupPerUsdCents) =>
        currency == Currency.USD ? amountCents / 100m : (decimal)amountCents / rateCupPerUsdCents;

    /// <summary>
    /// Fills every computed field of the lines and the goods total. Charges are spread BY VALUE:
    /// a line worth 70% of the goods carries 70% of the freight (decision 2026-11-12).
    /// Expects factors already resolved and stock quantities exact (the service checks both).
    /// </summary>
    public static void Calculate(Purchase purchase)
    {
        var rate = purchase.RateCupPerUsdCents;
        var lineUsd = new List<decimal>(purchase.Lines.Count);

        foreach (var line in purchase.Lines)
        {
            line.StockQuantityMilli = StockQuantityMilli(line.QuantityMilli, line.ConversionFactorMilli!.Value)
                                      ?? throw new InvalidOperationException("Stock quantity is not exact.");
            line.LineTotalCents = Math.Max(1, Units.RoundToLong(line.QuantityMilli * (decimal)line.UnitCostCents
                                                                / Units.MilliPerUnit));
            lineUsd.Add(ToUsd(line.LineTotalCents, purchase.Currency, rate));
        }

        purchase.GoodsTotalCents = purchase.Lines.Sum(line => line.LineTotalCents);

        var goodsUsd = lineUsd.Sum();
        var chargesUsd = purchase.Charges.Sum(charge => ToUsd(charge.AmountCents, charge.Currency, rate));

        for (var i = 0; i < purchase.Lines.Count; i++)
        {
            var line = purchase.Lines[i];
            var allocatedUsd = goodsUsd == 0 ? 0 : chargesUsd * lineUsd[i] / goodsUsd;
            var unitUsd = (lineUsd[i] + allocatedUsd) / (line.StockQuantityMilli / (decimal)Units.MilliPerUnit);

            line.AllocatedChargesUsdMicros = Units.RoundToLong(allocatedUsd * Units.MicrosPerUnit);
            line.LandedUnitCostUsdMicros = Math.Max(1, Units.RoundToLong(unitUsd * Units.MicrosPerUnit));
            line.LandedUnitCostCupMicros = Math.Max(1, Units.RoundToLong(unitUsd * rate / 100m * Units.MicrosPerUnit));
        }
    }
}

/// <summary>A stock movement as the cost replay sees it.</summary>
public sealed record CostMovement(long QuantityMilli, long? UnitCostUsdMicros, long? UnitCostCupMicros);

/// <summary>
/// A cost per stock unit in USD and CUP: the weighted average (RN-01, RN-02) or the last purchase's landed cost (D-52).
/// </summary>
public sealed record UnitCost(long UsdMicros, long CupMicros);

public static class CostCalculator
{
    /// <summary>
    /// Replays the movements IN ORDER (date, then creation) applying RN-01/RN-02 to each costed entry:
    ///   new average = (stock × average + quantity × cost) / (stock + quantity)
    /// The average uses the stock EXISTING at that moment. With no stock (zero or negative, e.g. sold before
    /// the purchase was typed in), the average restarts at the incoming cost. Outgoing rows never move it.
    /// Replaying from scratch makes voiding exact: drop the voided rows and replay; nothing to "un-average".
    /// Null when no costed entry exists.
    /// </summary>
    public static UnitCost? Replay(IEnumerable<CostMovement> movements)
    {
        decimal stock = 0;
        decimal? usd = null;
        decimal? cup = null;

        foreach (var movement in movements)
        {
            if (movement is { QuantityMilli: > 0, UnitCostUsdMicros: { } inUsd, UnitCostCupMicros: { } inCup })
            {
                if (stock <= 0 || usd is null || cup is null)
                {
                    usd = inUsd;
                    cup = inCup;
                }
                else
                {
                    var total = stock + movement.QuantityMilli;
                    usd = (stock * usd.Value + movement.QuantityMilli * (decimal)inUsd) / total;
                    cup = (stock * cup.Value + movement.QuantityMilli * (decimal)inCup) / total;
                }
            }

            stock += movement.QuantityMilli;
        }

        return usd is null || cup is null
            ? null
            : new UnitCost(Units.RoundToLong(usd.Value), Units.RoundToLong(cup.Value));
    }
}

public static class PriceCalculator
{
    /// <summary>The purchase rate of a cost = CUP cost / USD cost, in CUP cents per USD (RN-02 for the average).</summary>
    public static long PurchaseRateCents(UnitCost cost) =>
        Units.RoundToLong(cost.CupMicros * 100m / cost.UsdMicros);

    /// <summary>
    /// RN-03: Cost USD × (purchase rate + colchón) × (1 + recargo) = (cost CUP + cost USD × colchón) × (1 + recargo),
    /// rounded UP to the business rounding step (264.12 with a step of 5 → 265): rounding down would eat margin.
    /// The cost given is the LAST one (D-52): the price must pay for restocking.
    /// </summary>
    public static long SuggestedPriceCents(UnitCost cost, long bufferCents, int markupBps, long roundingStepCents)
    {
        var baseCents = cost.CupMicros / 10_000m + cost.UsdMicros * (decimal)bufferCents / Units.MicrosPerUnit;
        // 4 decimals first: a division artifact like 26500.0000000001 must not jump to the next step.
        var priced = Math.Round(baseCents * (10_000 + markupBps) / 10_000m, 4);
        var step = Math.Max(1, roundingStepCents);
        return (long)(decimal.Ceiling(priced / step) * step);
    }

    /// <summary>(price − CUP cost) / price, in basis points. Null when there is no price.</summary>
    public static int? MarginBps(long? salePriceCents, UnitCost cost)
    {
        if (salePriceCents is not > 0)
        {
            return null;
        }

        var costCents = cost.CupMicros / 10_000m;
        return (int)Units.RoundToLong((salePriceCents.Value - costCents) * 10_000 / salePriceCents.Value);
    }

    /// <summary>RN-04: today's rate above purchase rate + colchón → the price needs a look.</summary>
    public static bool IsBufferExceeded(long? todayRateCents, long purchaseRateCents, long bufferCents) =>
        todayRateCents is { } today && today > purchaseRateCents + bufferCents;
}

/// <summary>
/// Everything the screens show about a product's cost and price. Computed, never stored.
/// D-52: the suggested price, the margin and the colchón alert use the LAST cost and its rate (they look ahead:
/// restocking); the average is shown beside it (it values what is already on the shelf).
/// </summary>
public sealed record ProductPricing(
    long AvgCostUsdMicros,
    long AvgCostCupMicros,
    long LastCostUsdMicros,
    long LastCostCupMicros,
    long CurrentCostUsdMicros,
    long CurrentCostCupMicros,
    long LastRateCents,
    long EffectiveBufferCents,
    int EffectiveMarkupBps,
    long SuggestedPriceCents,
    int? MarginBps,
    long? TodayRateCents,
    bool IsBufferExceeded);

public static class ProductPricingRules
{
    /// <summary>
    /// Effective colchón / recargo = the product's own, else the business default (D-07).
    /// Null when the product has no cost yet (never bought).
    /// </summary>
    public static ProductPricing? Build(Product product, BusinessSettings settings, long? todayRateCents)
    {
        if (product.AverageCost is not { } average)
        {
            return null;
        }

        var last = product.LastCost ?? average;
        var current = product.CurrentCost ?? last;
        var buffer = product.ExchangeBufferCents ?? settings.ExchangeBufferCents;
        var markup = product.MarkupBps ?? settings.DefaultMarkupBps;
        var lastRate = PriceCalculator.PurchaseRateCents(last);

        return new ProductPricing(
            average.UsdMicros,
            average.CupMicros,
            last.UsdMicros,
            last.CupMicros,
            current.UsdMicros,
            current.CupMicros,
            lastRate,
            buffer,
            markup,
            PriceCalculator.SuggestedPriceCents(last, buffer, markup, settings.PriceRoundingStepCents),
            PriceCalculator.MarginBps(product.SalePriceCents, last),
            todayRateCents,
            PriceCalculator.IsBufferExceeded(todayRateCents, lastRate, buffer));
    }
}
