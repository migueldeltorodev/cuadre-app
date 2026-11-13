namespace Cuadre.Application.Models;

// Slice 6b: what comes after the daily closing. Days left to close (RF-CIE-09) and the Profit First evaluation of
// a period (RF-PF-10).

/// <summary>
/// RF-CIE-09: business days already over (up to yesterday) that are still not closed. Only the days after the last
/// closing count: earlier ones are locked anyway. Days the business is closed are skipped.
/// </summary>
public sealed record PendingClosingDays(DateOnly? LastClosedDate, int Count, IReadOnlyList<DateOnly> Days);

public static class ClosingCalendar
{
    /// <summary>At most this many days are listed; <see cref="PendingClosingDays.Count"/> still counts them all.</summary>
    public const int MaxListed = 31;

    /// <summary>
    /// From the day after the last closing (or the first day with activity, when nothing was closed yet) to
    /// yesterday, without the weekdays the business is closed. Oldest first. No activity and no closing: nothing.
    /// </summary>
    public static IReadOnlyList<DateOnly> PendingDays(DateOnly? lastClosed, DateOnly? firstActivity, DateOnly today,
        IReadOnlyCollection<DayOfWeek> closedDays)
    {
        var start = lastClosed?.AddDays(1) ?? firstActivity;
        if (start is not { } day)
        {
            return [];
        }

        var days = new List<DateOnly>();
        for (; day < today; day = day.AddDays(1))
        {
            if (!closedDays.Contains(day.DayOfWeek))
            {
                days.Add(day);
            }
        }

        return days;
    }
}

public class GetEvaluationOptions
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
}

/// <summary>
/// RF-PF-10: how the money of the confirmed closings in [From, To] was split, against the plan. A1 = collected,
/// A2 = cost of sales, A3 = A1 − A2 (D-02, D-03). Recoveries of shortages count on the day they came in.
/// </summary>
public sealed record ProfitFirstEvaluation(
    DateOnly From,
    DateOnly To,
    int ClosedDays,
    long CashCollectedCents,
    long TransferCollectedCents,
    long CostOfSalesCents,
    long TheoreticalSalesCents,
    long StaffPayCents,
    long ShortageCents,
    long SurplusCents,
    long RecoveredCents,
    long InternalDebtCents,
    IReadOnlyList<EnvelopeEvaluation> Envelopes,
    IReadOnlyList<ExpenseTotal> Expenses)
{
    /// <summary>A1.</summary>
    public long CollectedCents => CashCollectedCents + TransferCollectedCents;

    /// <summary>A3.</summary>
    public long RealIncomeCents => CollectedCents - CostOfSalesCents;

    /// <summary>A2 / A1 and A3 / A1, in basis points. Null without collections.</summary>
    public int? CostRatioBps => Ratio(CostOfSalesCents, CollectedCents);

    public int? RealIncomeRatioBps => Ratio(RealIncomeCents, CollectedCents);

    internal static int? Ratio(long part, long whole) =>
        whole == 0 ? null : (int)Math.Round(part * 10_000m / whole, MidpointRounding.AwayFromZero);
}

/// <summary>
/// One envelope in the period. Reposición has no target: it gets what is left. The real % is over what the %
/// envelopes received together, so it compares with the plan.
/// </summary>
public sealed record EnvelopeEvaluation(
    Guid EnvelopeId,
    string Name,
    EnvelopeRole Role,
    int? TargetPercentBps,
    long ReceivedCents,
    int? RealPercentBps);

/// <summary>Raw sums read from the database; the service turns them into the evaluation.</summary>
public class EvaluationTotals
{
    public long ClosedDays { get; init; }
    public long CashCollectedCents { get; init; }
    public long TransferCollectedCents { get; init; }
    public long CostOfSalesCents { get; init; }
    public long TheoreticalSalesCents { get; init; }
    public long StaffPayCents { get; init; }
    public long ShortageCents { get; init; }
    public long SurplusCents { get; init; }
    public long RecoveredCents { get; init; }
}

/// <summary>What one envelope received in the period: closing splits plus recoveries.</summary>
public class EnvelopeReceived
{
    public Guid EnvelopeId { get; init; }
    public long AmountCents { get; init; }
}
