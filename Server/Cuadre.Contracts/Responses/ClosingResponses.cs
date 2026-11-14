using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

// Money in CUP cents; quantities in thousandths of the stock unit; percentages in basis points.

/// <summary>A product on a counter that day, before counting (the "planilla").</summary>
public class ClosingCounterLineResponse
{
    public required Guid ProductId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required bool AllowsDecimals { get; init; }
    public required Guid LocationId { get; init; }
    public required string LocationName { get; init; }
    public required long OpeningMilli { get; init; }
    public required long InMilli { get; init; }
    public required long OutMilli { get; init; }

    /// <summary>Opening + in − out: what should be there if nothing was sold.</summary>
    public required long ExpectedMilli { get; init; }

    /// <summary>The price in force that day. Null = never priced (it cannot be sold).</summary>
    public long? UnitPriceCents { get; init; }

    public required int ToleranceBps { get; init; }
}

public class ClosingPaymentMethodResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid CashBoxId { get; init; }
    public required string CashBoxName { get; init; }
    public required CashBoxType CashBoxKind { get; init; }

    /// <summary>It lands in a bank box: "plata atrapada".</summary>
    public required bool IsTransfer { get; init; }
}

/// <summary>Everything the closing screens need before the user types.</summary>
public class DailyClosingDraftResponse
{
    public required DateOnly Date { get; init; }

    /// <summary>A day the business usually does not open (a warning, not a block).</summary>
    public required bool IsClosedDay { get; init; }

    public DateOnly? LastClosedDate { get; init; }

    /// <summary>The day is already closed: this is its closing.</summary>
    public Guid? ExistingClosingId { get; init; }

    public required bool IsExchangeRateMissing { get; init; }

    /// <summary>Why it cannot be closed. Null = it can.</summary>
    public string? Problem { get; init; }

    public required bool CanClose { get; init; }
    public required IReadOnlyList<ClosingCounterLineResponse> Lines { get; init; } = [];
    public required IReadOnlyList<ClosingPaymentMethodResponse> PaymentMethods { get; init; } = [];
    public required StaffPaySchemeResponse PayScheme { get; init; }

    /// <summary>Internal debt of Reposición at the end of the previous day.</summary>
    public required long DebtBeforeCents { get; init; }

    /// <summary>Above it (after the accepted shrinkage) the cash difference needs a note.</summary>
    public required long ThresholdCents { get; init; }
}

public class DailyClosingLineResponse
{
    public required Guid Id { get; init; }
    public required int LineNo { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductCode { get; init; }
    public required string ProductName { get; init; }
    public required Guid LocationId { get; init; }
    public required string LocationName { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required long OpeningMilli { get; init; }
    public required long InMilli { get; init; }
    public required long OutMilli { get; init; }
    public required long ExpectedMilli { get; init; }
    public required long CountedMilli { get; init; }

    /// <summary>Expected − counted. Negative = more than expected was found.</summary>
    public required long SoldMilli { get; init; }

    public long? UnitPriceCents { get; init; }
    public required long SalesCents { get; init; }
    public required int ToleranceBps { get; init; }
    public required long ToleranceCents { get; init; }
    public required long CostCents { get; init; }

    /// <summary>The colchón part of CostCents (D-60): USD cost × colchón. Reposición keeps it.</summary>
    public long BufferCents { get; init; }
}

public class DailyClosingCollectionResponse
{
    public required Guid PaymentMethodId { get; init; }
    public required string PaymentMethodName { get; init; }
    public required Guid CashBoxId { get; init; }
    public required string CashBoxName { get; init; }
    public required bool IsTransfer { get; init; }
    public required long AmountCents { get; init; }
}

/// <summary>What one envelope got from the day.</summary>
public class DailyClosingAllocationResponse
{
    public required Guid EnvelopeId { get; init; }
    public required string EnvelopeName { get; init; }
    public required EnvelopeType Role { get; init; }

    /// <summary>Null for Reposición: it gets A2, not a %.</summary>
    public int? PercentBps { get; init; }

    public required long AmountCents { get; init; }

    /// <summary>Cash to put physically in the envelope (Operaciones: minus the staff pay already handed over).</summary>
    public required long CashCents { get; init; }

    /// <summary>Already in the bank (only Reposición).</summary>
    public required long BankCents { get; init; }
}

/// <summary>A part of a shortage that came back, on the day it came back.</summary>
public class ShortageRecoveryResponse
{
    public required Guid Id { get; init; }
    public required DateOnly Date { get; init; }
    public required Guid CashBoxId { get; init; }
    public required string CashBoxName { get; init; }
    public required long AmountCents { get; init; }
    public string? Note { get; init; }
    public required ShortageRecoveryState Status { get; init; }
    public string? VoidReason { get; init; }
    public DateTime? VoidedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public class CashDiscrepancyResponse
{
    public required Guid Id { get; init; }
    public required Guid ClosingId { get; init; }

    /// <summary>The day of the closing where it came out.</summary>
    public required DateOnly Date { get; init; }

    public required CashDiscrepancyType Kind { get; init; }
    public required long AmountCents { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string? Note { get; init; }
    public required CashDiscrepancyState Status { get; init; }

    /// <summary>Confirmed recoveries so far.</summary>
    public required long RecoveredCents { get; init; }

    /// <summary>What is still to claim (0 unless Pending).</summary>
    public required long PendingCents { get; init; }

    /// <summary>Why it was written off.</summary>
    public string? ResolutionNote { get; init; }

    public DateTime? ResolvedAt { get; init; }

    /// <summary>Only in the detail of a shortage; empty in lists and inside a closing.</summary>
    public required IReadOnlyList<ShortageRecoveryResponse> Recoveries { get; init; } = [];

    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class CashDiscrepanciesResponse : PagedResponse<CashDiscrepancyResponse>;

/// <summary>Shortages still to claim, for the menu and the home page.</summary>
public class PendingShortagesResponse
{
    public required int Count { get; init; }
    public required long PendingCents { get; init; }
}

/// <summary>RF-CIE-09: business days already over and not closed (at most 31 listed; Count has them all).</summary>
public class PendingClosingDaysResponse
{
    public DateOnly? LastClosedDate { get; init; }
    public required int Count { get; init; }
    public required IReadOnlyList<DateOnly> Days { get; init; } = [];
}

/// <summary>A closing, or its preview (then Id is the one it would get and nothing is saved).</summary>
public class DailyClosingResponse
{
    public required Guid Id { get; init; }
    public required DateOnly Date { get; init; }
    public required DailyClosingState Status { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string? Notes { get; init; }

    public required long TheoreticalSalesCents { get; init; }
    public required long ShrinkageToleranceCents { get; init; }
    public required long CashCollectedCents { get; init; }
    public required long TransferCollectedCents { get; init; }

    /// <summary>A1: what was really collected.</summary>
    public required long CollectedCents { get; init; }

    /// <summary>A2: cost of what was sold.</summary>
    public required long CostOfSalesCents { get; init; }

    /// <summary>A3 = A1 − A2.</summary>
    public required long RealIncomeCents { get; init; }

    /// <summary>A1 − theoretical sales. Negative = shortage.</summary>
    public required long CashDifferenceCents { get; init; }

    /// <summary>The difference after the accepted shrinkage.</summary>
    public required long AdjustedDifferenceCents { get; init; }

    public required long ThresholdCents { get; init; }
    public required bool RequiresNote { get; init; }

    public required long StaffPayCents { get; init; }
    public required long PayFixedCents { get; init; }
    public required int PayPercentBps { get; init; }

    public required long DebtBeforeCents { get; init; }
    public required long DebtCreatedCents { get; init; }
    public required long DebtRepaidCents { get; init; }
    public required long DebtAfterCents { get; init; }

    public string? VoidReason { get; init; }
    public DateTime? VoidedAt { get; init; }

    public required IReadOnlyList<DailyClosingLineResponse> Lines { get; init; } = [];
    public required IReadOnlyList<DailyClosingCollectionResponse> Collections { get; init; } = [];
    public required IReadOnlyList<DailyClosingAllocationResponse> Allocations { get; init; } = [];
    public CashDiscrepancyResponse? Discrepancy { get; init; }

    /// <summary>
    /// RF-CIE-11, preview and create only: another closed day has exactly the same collections and sales. A warning:
    /// it may be the same day loaded twice.
    /// </summary>
    public DateOnly? PossibleDuplicateOf { get; init; }

    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class DailyClosingSummaryResponse
{
    public required Guid Id { get; init; }
    public required DateOnly Date { get; init; }
    public required DailyClosingState Status { get; init; }
    public string? EmployeeName { get; init; }
    public required long CollectedCents { get; init; }
    public required long TransferCollectedCents { get; init; }
    public required long CostOfSalesCents { get; init; }
    public required long RealIncomeCents { get; init; }
    public required long AdjustedDifferenceCents { get; init; }
    public required long StaffPayCents { get; init; }
    public required long DebtAfterCents { get; init; }
}

public class DailyClosingsResponse : PagedResponse<DailyClosingSummaryResponse>;
