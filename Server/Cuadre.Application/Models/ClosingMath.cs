namespace Cuadre.Application.Models;

// Pure math of the daily closing (slice 6): no database, no clock. Every rule is unit tested.
// Money in CUP cents, quantities in thousandths of the stock unit, unit costs in millionths.

/// <summary>One counted line, as the math sees it.</summary>
public sealed record CountInput(
    long OpeningMilli,
    long InMilli,
    long OutMilli,
    long CountedMilli,
    long? UnitPriceCents,
    int ToleranceBps,
    long? UnitCostCupMicros);

/// <summary>What one counted line means (RN-05, RN-06, RN-06b, RN-07).</summary>
public sealed record CountResult(
    long ExpectedMilli,
    long SoldMilli,
    long SalesCents,
    long ToleranceCents,
    long CostCents);

/// <summary>Everything the closing collected, computed and owes. Money only: no ids, no names.</summary>
public sealed record ClosingTotals(
    long TheoreticalSalesCents,
    long ToleranceCents,
    long CashCollectedCents,
    long TransferCollectedCents,
    long CostOfSalesCents,
    long CashDifferenceCents,
    long AdjustedDifferenceCents,
    long StaffPayCents)
{
    /// <summary>A1: what was really collected (D-02).</summary>
    public long CollectedCents => CashCollectedCents + TransferCollectedCents;

    /// <summary>A3 = A1 − A2 (D-03). May be negative on a bad day: the envelopes then go down.</summary>
    public long RealIncomeCents => CollectedCents - CostOfSalesCents;
}

/// <summary>
/// RN-10 for one day. "Others" = every envelope with a % (not Reposición).
/// Reposición gets A1 − ToOthers: all the transfers plus the cash it still needs.
/// </summary>
public sealed record ClosingSplit(
    long ToOthersCents,
    long DebtCreatedCents,
    long DebtRepaidCents,
    long DebtAfterCents);

/// <summary>A piece of the split: so much money of one envelope, in one cash box.</summary>
public sealed record BoxShare(Guid CashBoxId, Guid EnvelopeId, long AmountCents);

public static class ClosingCalculator
{
    private const long CentsPerPeso = 100;
    private const decimal FullBps = 10_000m;

    /// <summary>
    /// RN-05: sold = opening + in − out − counted. Its value at the price of the day (RN-06), the shrinkage it may
    /// hide (only on what was sold: a % of its sales) and its historical cost (RN-07, A2).
    /// A negative "sold" (more on the counter than there should be) lowers the sales and the cost; no tolerance.
    /// </summary>
    public static CountResult Count(CountInput input)
    {
        var expected = input.OpeningMilli + input.InMilli - input.OutMilli;
        var sold = expected - input.CountedMilli;
        var sales = Units.RoundToLong(sold * (decimal)(input.UnitPriceCents ?? 0) / Units.MilliPerUnit);
        var tolerance = sold > 0 && sales > 0 ? Units.RoundToLong(sales * input.ToleranceBps / FullBps) : 0;
        var cost = Units.RoundToLong(sold * (decimal)(input.UnitCostCupMicros ?? 0) / 10_000_000m);
        return new CountResult(expected, sold, sales, tolerance, cost);
    }

    /// <summary>
    /// RN-06 / RN-06c: difference = A1 − theoretical sales. A shortage is first covered by the accepted shrinkage
    /// (bulk weighing); only what is left is a shortage. A surplus is recorded as it is.
    /// </summary>
    public static long AdjustedDifference(long differenceCents, long toleranceCents) =>
        differenceCents >= 0 ? differenceCents : Math.Min(0, differenceCents + toleranceCents);

    /// <summary>RN-08: fixed + % of A1, rounded to whole pesos (it is paid in hand).</summary>
    public static long StaffPay(long fixedCents, int percentBps, long collectedCents)
    {
        var exact = fixedCents + Math.Max(0, collectedCents) * (decimal)percentBps / FullBps;
        return Units.RoundToLong(exact / CentsPerPeso) * CentsPerPeso;
    }

    public static ClosingTotals Totals(IEnumerable<CountResult> lines, long cashCollectedCents,
        long transferCollectedCents, long payFixedCents, int payPercentBps)
    {
        var counted = lines.ToList();
        var theoretical = counted.Sum(line => line.SalesCents);
        var tolerance = counted.Sum(line => line.ToleranceCents);
        var collected = cashCollectedCents + transferCollectedCents;
        var difference = collected - theoretical;

        return new ClosingTotals(
            theoretical,
            tolerance,
            cashCollectedCents,
            transferCollectedCents,
            counted.Sum(line => line.CostCents),
            difference,
            AdjustedDifference(difference, tolerance),
            StaffPay(payFixedCents, payPercentBps, collected));
    }

    /// <summary>
    /// RN-10 (D-11), with the internal debt of RF-PF-09:
    ///  1. Every transfer goes to Reposición (the bank money is "trapped" there).
    ///  2. Reposición completes A2 with cash.
    ///  3. The rest of the cash goes to the other envelopes.
    ///  4. Transfers above A2: the excess stays in Reposición and it OWES it to the others (debt created).
    ///  5. An open debt is paid back first with the cash that Reposición gets on the next days (debt repaid).
    /// On a day with A3 &lt; 0 nothing is paid back and the others receive a negative amount (they give to Reposición).
    /// </summary>
    public static ClosingSplit Split(ClosingTotals totals, long debtBeforeCents)
    {
        var a2 = totals.CostOfSalesCents;
        var transfers = totals.TransferCollectedCents;
        var a3 = totals.RealIncomeCents;

        var debtCreated = Math.Max(0, transfers - a2);
        var replenishmentCash = Math.Max(0, a2 - transfers);
        var repaid = a3 > 0 ? Math.Min(Math.Max(0, debtBeforeCents), replenishmentCash) : 0;

        return new ClosingSplit(
            a3 - debtCreated + repaid,
            debtCreated,
            repaid,
            Math.Max(0, debtBeforeCents) - repaid + debtCreated);
    }

    /// <summary>
    /// Splits an amount by basis points so the parts add up EXACTLY to it (largest remainder method).
    /// The leftover cents go to the largest remainders; ties to the earlier item. Works for negative amounts too.
    /// </summary>
    public static IReadOnlyList<long> Apportion(long amountCents, IReadOnlyList<int> percentsBps)
    {
        if (percentsBps.Count == 0)
        {
            return [];
        }

        var totalBps = percentsBps.Sum();
        if (totalBps <= 0)
        {
            return percentsBps.Select(_ => 0L).ToList();
        }

        var sign = Math.Sign(amountCents);
        var absolute = Math.Abs(amountCents);
        var exact = percentsBps.Select(bps => absolute * (decimal)bps / totalBps).ToList();
        var parts = exact.Select(value => (long)decimal.Floor(value)).ToArray();
        var leftover = absolute - parts.Sum();

        foreach (var index in exact
                     .Select((value, index) => (Remainder: value - decimal.Floor(value), Index: index))
                     .OrderByDescending(x => x.Remainder)
                     .ThenBy(x => x.Index)
                     .Take((int)leftover)
                     .Select(x => x.Index))
        {
            parts[index]++;
        }

        return parts.Select(part => part * sign).ToList();
    }

    /// <summary>
    /// Where each envelope's cash is taken from: the cash collected today, box by box (the primary box first), so a
    /// box never hands out more than it received. Negative shares (a day with A3 &lt; 0) go back in the primary box.
    /// The result is per (box, envelope); zero pieces are left out.
    /// </summary>
    public static IReadOnlyList<BoxShare> AssignBoxes(IReadOnlyList<(Guid CashBoxId, long AmountCents)> cashByBox,
        Guid primaryBoxId, IReadOnlyList<(Guid EnvelopeId, long AmountCents)> shares)
    {
        var available = cashByBox
            .Where(box => box.AmountCents > 0)
            .OrderByDescending(box => box.CashBoxId == primaryBoxId)
            .Select(box => (box.CashBoxId, Left: box.AmountCents))
            .ToList();

        var result = new List<BoxShare>();
        foreach (var (envelopeId, amount) in shares)
        {
            if (amount <= 0)
            {
                if (amount < 0)
                {
                    result.Add(new BoxShare(primaryBoxId, envelopeId, amount));
                }

                continue;
            }

            var pending = amount;
            for (var i = 0; i < available.Count && pending > 0; i++)
            {
                var take = Math.Min(pending, available[i].Left);
                if (take <= 0)
                {
                    continue;
                }

                result.Add(new BoxShare(available[i].CashBoxId, envelopeId, take));
                available[i] = (available[i].CashBoxId, available[i].Left - take);
                pending -= take;
            }

            // More to give than cash collected (only with an inconsistent input): the rest from the primary box.
            if (pending > 0)
            {
                result.Add(new BoxShare(primaryBoxId, envelopeId, pending));
            }
        }

        return result
            .GroupBy(share => (share.CashBoxId, share.EnvelopeId))
            .Select(group => new BoxShare(group.Key.CashBoxId, group.Key.EnvelopeId, group.Sum(s => s.AmountCents)))
            .Where(share => share.AmountCents != 0)
            .ToList();
    }
}
