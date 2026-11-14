namespace Cuadre.Application.Models;

/// <summary>
/// The close of one business day (RF-CIE): counter count → sales and A2, collections → A1, staff pay, the split into
/// the envelopes per cash box and the internal debt. Its figures are a SNAPSHOT of that day.
/// Never edited. Reopening = voiding it: the reversal movements are dated the same day (D-27).
/// Money in CUP cents (the closing only collects in CUP, D-28).
/// </summary>
public class DailyClosing
{
    public required Guid Id { get; init; }
    public required DateOnly Date { get; init; }
    public DailyClosingStatus Status { get; set; }
    public Guid? EmployeeId { get; init; }
    public string? Notes { get; init; }

    public long TheoreticalSalesCents { get; set; }
    public long ShrinkageToleranceCents { get; set; }
    public long CashCollectedCents { get; set; }
    public long TransferCollectedCents { get; set; }
    public long CostOfSalesCents { get; set; }
    public long CashDifferenceCents { get; set; }
    public long AdjustedDifferenceCents { get; set; }
    public long ThresholdCents { get; set; }

    public long StaffPayCents { get; set; }
    public long PayFixedCents { get; set; }
    public int PayPercentBps { get; set; }

    public long DebtBeforeCents { get; set; }
    public long DebtCreatedCents { get; set; }
    public long DebtRepaidCents { get; set; }
    public long DebtAfterCents { get; set; }

    public string? VoidReason { get; set; }
    public DateTime? VoidedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<DailyClosingLine> Lines { get; init; } = [];
    public List<DailyClosingCollection> Collections { get; init; } = [];
    public List<DailyClosingAllocation> Allocations { get; init; } = [];
    public CashDiscrepancy? Discrepancy { get; set; }

    /// <summary>Read-only, filled by queries.</summary>
    public string? EmployeeName { get; init; }

    /// <summary>
    /// RF-CIE-11, preview and create only (not stored): another closed day with exactly the same collections and
    /// theoretical sales. A warning: it may be the same day loaded twice.
    /// </summary>
    public DateOnly? PossibleDuplicateOf { get; set; }

    /// <summary>A1 (D-02).</summary>
    public long CollectedCents => CashCollectedCents + TransferCollectedCents;

    /// <summary>A3 = A1 − A2 (D-03).</summary>
    public long RealIncomeCents => CollectedCents - CostOfSalesCents;

    /// <summary>RF-CIE-04: above the threshold (after the accepted shrinkage) a note is required.</summary>
    public bool RequiresNote => Math.Abs(AdjustedDifferenceCents) > ThresholdCents;
}

/// <summary>Voided = reopened (RF-CIE-10). Stored by name.</summary>
public enum DailyClosingStatus
{
    Confirmed,
    Voided
}

/// <summary>One counter line (RN-05). Units and costs are a snapshot.</summary>
public class DailyClosingLine
{
    public required Guid Id { get; init; }
    public Guid ClosingId { get; set; }
    public int LineNo { get; set; }
    public required Guid ProductId { get; init; }
    public required Guid LocationId { get; init; }
    public MeasureUnit StockUnit { get; init; }
    public long OpeningMilli { get; init; }
    public long InMilli { get; init; }
    public long OutMilli { get; init; }
    public long ExpectedMilli { get; init; }
    public long CountedMilli { get; init; }
    public long SoldMilli { get; init; }
    public long? UnitPriceCents { get; init; }
    public long SalesCents { get; init; }
    public int ToleranceBps { get; init; }
    public long ToleranceCents { get; init; }
    /// <summary>D-60: the FIFO cost per unit of what sold, at its purchase rates (blended when it took several batches).</summary>
    public long? UnitCostUsdMicros { get; init; }

    public long? UnitCostCupMicros { get; init; }

    /// <summary>A2 of the line: real FIFO cost + <see cref="BufferCents"/>.</summary>
    public long CostCents { get; init; }

    /// <summary>The colchón part of <see cref="CostCents"/>: USD cost × colchón (0 before D-60).</summary>
    public long BufferCents { get; init; }

    // Read-only, filled by queries.
    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string LocationName { get; init; } = "";
}

public class DailyClosingCollection
{
    public Guid ClosingId { get; set; }
    public required Guid PaymentMethodId { get; init; }
    public Guid CashBoxId { get; init; }

    /// <summary>It landed in a Bank box: "plata atrapada" (D-11).</summary>
    public bool IsTransfer { get; init; }

    public required long AmountCents { get; init; }

    // Read-only, filled by queries.
    public string PaymentMethodName { get; init; } = "";
    public string CashBoxName { get; init; } = "";
}

/// <summary>What one envelope got from the day: in total, as cash to put in it, and already in the bank.</summary>
public class DailyClosingAllocation
{
    public Guid ClosingId { get; set; }
    public required Guid EnvelopeId { get; init; }

    /// <summary>Null for Reposición.</summary>
    public int? PercentBps { get; init; }

    public long AmountCents { get; init; }

    /// <summary>
    /// Cash to put physically in the envelope. Operations: its share minus the staff pay, already handed over.
    /// </summary>
    public long CashCents { get; init; }

    public long BankCents { get; init; }

    // Read-only, filled by queries.
    public string EnvelopeName { get; init; } = "";
    public EnvelopeRole Role { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// Faltante / sobrante of the cash count (RN-06c). A shortage is claimed: recovered in one or more parts, or written
/// off as lost. A surplus is only recorded.
/// </summary>
public class CashDiscrepancy
{
    public required Guid Id { get; init; }
    public Guid ClosingId { get; set; }
    public required DateOnly Date { get; init; }
    public required CashDiscrepancyKind Kind { get; init; }
    public required long AmountCents { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? Note { get; init; }
    public CashDiscrepancyStatus Status { get; set; }

    /// <summary>Why it was written off (Lost).</summary>
    public string? ResolutionNote { get; set; }

    /// <summary>When it became Recovered or Lost.</summary>
    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Read-only, filled by queries.
    public long RecoveredCents { get; init; }
    public DateOnly? ClosingDate { get; init; }
    public string? EmployeeName { get; init; }
    public List<ShortageRecovery> Recoveries { get; init; } = [];

    /// <summary>What is still to be claimed (0 once recovered or lost).</summary>
    public long PendingCents => Status == CashDiscrepancyStatus.Pending ? AmountCents - RecoveredCents : 0;
}

/// <summary>Part of a shortage that came back (RN-06c): it comes in the day it is recovered. Never edited: voided.</summary>
public class ShortageRecovery
{
    public required Guid Id { get; init; }
    public required Guid DiscrepancyId { get; init; }
    public required DateOnly Date { get; init; }
    public required Guid CashBoxId { get; init; }
    public required long AmountCents { get; init; }
    public string? Note { get; init; }
    public ShortageRecoveryStatus Status { get; set; }
    public string? VoidReason { get; set; }
    public DateTime? VoidedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Read-only, filled by queries.</summary>
    public string CashBoxName { get; init; } = "";
}

/// <summary>Stored by name.</summary>
public enum ShortageRecoveryStatus
{
    Confirmed,
    Voided
}

/// <summary>What the user brings to register a recovery.</summary>
public sealed record RecoverShortage(Guid DiscrepancyId, DateOnly? Date, Guid CashBoxId, long AmountCents,
    string? Note);

public sealed record WriteOffShortage(Guid DiscrepancyId, string Note);

public sealed record VoidShortageRecovery(Guid RecoveryId, string Reason);

public class GetAllCashDiscrepanciesOptions
{
    public CashDiscrepancyKind? Kind { get; init; }
    public CashDiscrepancyStatus? Status { get; init; }
    public Guid? EmployeeId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public enum CashDiscrepancyKind
{
    Shortage,
    Surplus
}

/// <summary>Stored by name, append only.</summary>
public enum CashDiscrepancyStatus
{
    /// <summary>A shortage to claim (maybe partly recovered already).</summary>
    Pending,

    /// <summary>A surplus: only kept in the history.</summary>
    Recorded,

    /// <summary>Its closing was reopened.</summary>
    Voided,

    /// <summary>A shortage fully recovered.</summary>
    Recovered,

    /// <summary>A shortage written off: what was not recovered is lost.</summary>
    Lost
}

/// <summary>What the user brings to close a day. Everything else is read or computed by the server.</summary>
public class DailyClosingInput
{
    public required DateOnly Date { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? Notes { get; init; }
    public List<ClosingCountInput> Counts { get; init; } = [];
    public List<ClosingCollectionInput> Collections { get; init; } = [];
}

public sealed record ClosingCountInput(Guid ProductId, Guid LocationId, long CountedMilli);

public sealed record ClosingCollectionInput(Guid PaymentMethodId, long AmountCents);

/// <summary>A product on a counter that day, before counting (the "planilla", RF-CIE-01).</summary>
public class CounterLine
{
    public Guid ProductId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public MeasureUnit StockUnit { get; init; }
    public Guid LocationId { get; init; }
    public string LocationName { get; init; } = "";

    /// <summary>At the end of the previous day.</summary>
    public long OpeningMilli { get; init; }

    /// <summary>Transfers in, purchases into the counter... of the day.</summary>
    public long InMilli { get; init; }

    /// <summary>Exits and transfers out of the day.</summary>
    public long OutMilli { get; init; }

    /// <summary>The price in force that day. Null = never priced.</summary>
    public long? UnitPriceCents { get; init; }

    public int ToleranceBps { get; init; }

    /// <summary>
    /// The product's own colchón (null = the business default). D-60 (RN-07): what sold is costed FIFO, batch by
    /// batch, and A2 adds USD cost × colchón, so Reposición keeps what the colchón charged in the price.
    /// </summary>
    public long? ExchangeBufferCents { get; init; }

    public long ExpectedMilli => OpeningMilli + InMilli - OutMilli;
    public bool AllowsDecimals => StockUnit.AllowsDecimals();
}

/// <summary>A payment method the closing asks for: active and landing in a CUP box (D-28).</summary>
public class ClosingPaymentMethod
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public Guid CashBoxId { get; init; }
    public string CashBoxName { get; init; } = "";
    public CashBoxKind CashBoxKind { get; init; }

    public bool IsTransfer => CashBoxKind == CashBoxKind.Bank;
}

/// <summary>Everything the closing screens need before the user types (GET draft).</summary>
public sealed record DailyClosingDraft(
    DateOnly Date,
    bool IsClosedDay,
    DateOnly? LastClosedDate,
    Guid? ExistingClosingId,
    bool IsExchangeRateMissing,
    string? Problem,
    IReadOnlyList<CounterLine> Lines,
    IReadOnlyList<ClosingPaymentMethod> PaymentMethods,
    StaffPayScheme PayScheme,
    long DebtBeforeCents,
    long ThresholdCents)
{
    public bool CanClose => Problem is null;
}

public class DailyClosingSummary
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public DailyClosingStatus Status { get; init; }
    public string? EmployeeName { get; init; }
    public long CashCollectedCents { get; init; }
    public long TransferCollectedCents { get; init; }
    public long CostOfSalesCents { get; init; }
    public long AdjustedDifferenceCents { get; init; }
    public long StaffPayCents { get; init; }
    public long DebtAfterCents { get; init; }

    public long CollectedCents => CashCollectedCents + TransferCollectedCents;
    public long RealIncomeCents => CollectedCents - CostOfSalesCents;
}

public class GetAllDailyClosingsOptions
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public bool IncludeVoided { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record ReopenDailyClosing(Guid ClosingId, string Reason);
