namespace Cuadre.Application.Models;

/// <summary>
/// A Profit First "sobre": what the money is FOR (D-10). Its balance is never stored: it is the SUM of its rows in
/// the cash ledger, per cash box (RF-PF-06). Never deleted; only the user's own envelopes can be deactivated.
/// </summary>
public class Envelope
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required EnvelopeRole Role { get; init; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Reposición receives A2 and never has a % (RF-PF-04).</summary>
    public bool TakesPercent => Role != EnvelopeRole.Replenishment;
}

/// <summary>
/// Stored by name, append only. Every role but Custom exists exactly once: the app's rules point at it
/// (purchases pay from Replenishment, house consumption moves OwnerPay → Replenishment, taxes leave Tax...).
/// </summary>
public enum EnvelopeRole
{
    Profit,
    OwnerPay,
    Tax,
    Operations,
    Replenishment,
    Custom
}

/// <summary>The only fields of an envelope that may change after creation.</summary>
public sealed record EnvelopeChanges(Guid Id, string Name, bool IsActive);

/// <summary>The % of A3 for each envelope from a date on (RF-PF-05). A change is a new plan.</summary>
public class AllocationPlan
{
    public required Guid Id { get; init; }
    public required DateOnly EffectiveFrom { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<AllocationPlanItem> Items { get; init; } = [];
}

public class AllocationPlanItem
{
    public Guid PlanId { get; set; }
    public required Guid EnvelopeId { get; init; }

    /// <summary>Basis points: 30% = 3000.</summary>
    public required int PercentBps { get; init; }

    /// <summary>Read-only, filled by queries.</summary>
    public string EnvelopeName { get; init; } = "";
}

/// <summary>
/// The balance of one envelope in one cash box. A class with init properties, not a positional record:
/// Dapper builds records through their constructor and skips the Guid type handler.
/// </summary>
public class EnvelopeBoxBalance
{
    public Guid EnvelopeId { get; init; }
    public Guid CashBoxId { get; init; }
    public long AmountCents { get; init; }
}

/// <summary>A cash box as a column of the envelopes table, with its total.</summary>
public sealed record EnvelopeBoxColumn(
    Guid Id,
    string Name,
    Currency Currency,
    CashBoxKind Kind,
    bool IsActive,
    long TotalCents);

/// <summary>One row of the envelopes table: its balance in each box (only non-zero boxes) and its current %.</summary>
public sealed record EnvelopeRow(
    Envelope Envelope,
    int? PercentBps,
    IReadOnlyList<EnvelopeBoxBalance> Balances);

/// <summary>
/// The envelopes × cash boxes table at the end of a day (RF-PF-06). Bank columns are the "plata atrapada"
/// (RF-PF-11): money that exists but cannot be spent as cash.
/// </summary>
public sealed record EnvelopeOverview(
    DateOnly Date,
    IReadOnlyList<EnvelopeBoxColumn> CashBoxes,
    IReadOnlyList<EnvelopeRow> Envelopes,
    AllocationPlan? CurrentPlan);

/// <summary>One row of the cash ledger, for people: with names and the number of the document behind it.</summary>
public class CashMovementEntry
{
    public Guid Id { get; init; }
    public Guid CashBoxId { get; init; }
    public string CashBoxName { get; init; } = "";
    public Currency Currency { get; init; }
    public Guid EnvelopeId { get; init; }
    public string EnvelopeName { get; init; } = "";
    public DateOnly Date { get; init; }
    public CashMovementKind Kind { get; init; }
    public long AmountCents { get; init; }
    public string Description { get; init; } = "";
    public Guid? PurchaseId { get; init; }
    public int? PurchaseNumber { get; init; }
    public Guid? EnvelopeOperationId { get; init; }
    public int? EnvelopeOperationNumber { get; init; }
    public Guid? StockOperationId { get; init; }
    public int? StockOperationNumber { get; init; }

    /// <summary>Its document was voided later (the row and its reversal both stay).</summary>
    public bool IsVoided { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>A ledger row with the balance of its envelope in that box right after it.</summary>
public sealed record EnvelopeLedgerEntry(CashMovementEntry Movement, long BoxBalanceCents);

/// <summary>History of one envelope (RF-PF-06): oldest first, balances per box (currencies are never added up).</summary>
public sealed record EnvelopeLedger(
    Envelope Envelope,
    IReadOnlyList<EnvelopeBoxBalance> OpeningBalances,
    IReadOnlyList<EnvelopeLedgerEntry> Entries,
    IReadOnlyList<EnvelopeBoxBalance> ClosingBalances);

public class GetEnvelopeLedgerOptions
{
    public required Guid EnvelopeId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? CashBoxId { get; init; }
}

public static class EnvelopeMath
{
    /// <summary>The % of a plan add up to exactly 100%.</summary>
    public const int FullPercentBps = 10_000;

    /// <summary>Running balance per box, in ledger order (the movements must come oldest first).</summary>
    public static List<EnvelopeLedgerEntry> RunningBalances(IEnumerable<EnvelopeBoxBalance> opening,
        IEnumerable<CashMovementEntry> movements)
    {
        var balances = opening.ToDictionary(balance => balance.CashBoxId, balance => balance.AmountCents);
        var entries = new List<EnvelopeLedgerEntry>();
        foreach (var movement in movements)
        {
            var balance = balances.GetValueOrDefault(movement.CashBoxId) + movement.AmountCents;
            balances[movement.CashBoxId] = balance;
            entries.Add(new EnvelopeLedgerEntry(movement, balance));
        }

        return entries;
    }
}
