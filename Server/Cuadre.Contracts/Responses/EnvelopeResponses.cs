using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

// Money in cents of the CASH BOX's currency (never added across currencies); percentages in basis points.

public class EnvelopeResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required EnvelopeType Role { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsActive { get; init; }

    /// <summary>False only for Reposición: it receives A2, not a %.</summary>
    public required bool TakesPercent { get; init; }
}

public class EnvelopesResponse
{
    public required IEnumerable<EnvelopeResponse> Items { get; init; } = [];
}

public class AllocationPlanItemResponse
{
    public required Guid EnvelopeId { get; init; }
    public required string EnvelopeName { get; init; }
    public required int PercentBps { get; init; }
}

public class AllocationPlanResponse
{
    public required Guid Id { get; init; }

    /// <summary>2000-01-01 = the first plan, "since always".</summary>
    public required DateOnly EffectiveFrom { get; init; }

    public string? Notes { get; init; }
    public required IReadOnlyList<AllocationPlanItemResponse> Items { get; init; } = [];
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class AllocationPlansResponse
{
    /// <summary>Newest first: the first one is the one in force from its date on.</summary>
    public required IEnumerable<AllocationPlanResponse> Items { get; init; } = [];
}

public class EnvelopeBoxBalanceResponse
{
    public required Guid CashBoxId { get; init; }
    public required long AmountCents { get; init; }
}

public class EnvelopeCashBoxResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required CurrencyCode Currency { get; init; }

    /// <summary>Bank = "plata atrapada" (RF-PF-11).</summary>
    public required CashBoxType Kind { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>The box balance: the sum of its envelopes.</summary>
    public required long TotalCents { get; init; }
}

public class EnvelopeBalanceResponse
{
    public required EnvelopeResponse Envelope { get; init; }

    /// <summary>The % of A3 in force that day. Null for Reposición.</summary>
    public int? PercentBps { get; init; }

    /// <summary>Only the boxes where it holds something.</summary>
    public required IReadOnlyList<EnvelopeBoxBalanceResponse> Balances { get; init; } = [];
}

/// <summary>Envelopes × cash boxes at the end of a day (RF-PF-06).</summary>
public class EnvelopeOverviewResponse
{
    public required DateOnly Date { get; init; }
    public required IReadOnlyList<EnvelopeCashBoxResponse> CashBoxes { get; init; } = [];
    public required IReadOnlyList<EnvelopeBalanceResponse> Envelopes { get; init; } = [];
    public AllocationPlanResponse? CurrentPlan { get; init; }

    /// <summary>
    /// What Reposición still owes itself at the end of that day (RN-10): days whose A2 was collected by transfer
    /// borrowed it from the other envelopes' cash. It is repaid by later closings.
    /// </summary>
    public required long InternalDebtCents { get; init; }
}

public class CashMovementResponse
{
    public required Guid Id { get; init; }
    public required Guid CashBoxId { get; init; }
    public required string CashBoxName { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required Guid EnvelopeId { get; init; }
    public required string EnvelopeName { get; init; }
    public required DateOnly Date { get; init; }
    public required CashMovementType Kind { get; init; }
    public required long AmountCents { get; init; }
    public required string Description { get; init; }
    public Guid? PurchaseId { get; init; }
    public int? PurchaseNumber { get; init; }
    public Guid? EnvelopeOperationId { get; init; }
    public int? EnvelopeOperationNumber { get; init; }
    public Guid? StockOperationId { get; init; }
    public int? StockOperationNumber { get; init; }
    public Guid? ClosingId { get; init; }
    public DateOnly? ClosingDate { get; init; }

    /// <summary>A shortage recovery, and the closing where that shortage came out.</summary>
    public Guid? RecoveryId { get; init; }

    public Guid? RecoveryClosingId { get; init; }

    /// <summary>A gasto (slice 7).</summary>
    public Guid? ExpenseId { get; init; }

    public int? ExpenseNumber { get; init; }
    public required bool IsVoided { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public class EnvelopeLedgerEntryResponse
{
    public required CashMovementResponse Movement { get; init; }

    /// <summary>The envelope's balance in that box right after this row.</summary>
    public required long BoxBalanceCents { get; init; }
}

public class EnvelopeLedgerResponse
{
    public required EnvelopeResponse Envelope { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public required IReadOnlyList<EnvelopeBoxBalanceResponse> OpeningBalances { get; init; } = [];

    /// <summary>Oldest first.</summary>
    public required IReadOnlyList<EnvelopeLedgerEntryResponse> Entries { get; init; } = [];

    public required IReadOnlyList<EnvelopeBoxBalanceResponse> ClosingBalances { get; init; } = [];
}

public class EnvelopeOperationLineResponse
{
    public required Guid Id { get; init; }
    public required int LineNo { get; init; }
    public required Guid CashBoxId { get; init; }
    public required string CashBoxName { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required Guid EnvelopeId { get; init; }
    public required string EnvelopeName { get; init; }

    /// <summary>Signed. Opening: target − balance it had (0 = it already matched).</summary>
    public required long AmountCents { get; init; }

    /// <summary>Opening only.</summary>
    public long? TargetCents { get; init; }
}

public class NegativeEnvelopeWarningResponse
{
    public required Guid EnvelopeId { get; init; }
    public required string EnvelopeName { get; init; }
    public required Guid CashBoxId { get; init; }
    public required string CashBoxName { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required long BalanceCents { get; init; }
}

public class EnvelopeOperationResponse
{
    public required Guid Id { get; init; }
    public required int Number { get; init; }
    public required EnvelopeOperationType Kind { get; init; }
    public required DateOnly Date { get; init; }
    public Guid? CashBoxId { get; init; }
    public string? CashBoxName { get; init; }
    public CurrencyCode? Currency { get; init; }
    public Guid? EnvelopeId { get; init; }
    public string? EnvelopeName { get; init; }
    public Guid? ToEnvelopeId { get; init; }
    public string? ToEnvelopeName { get; init; }
    public long? AmountCents { get; init; }
    public string? Concept { get; init; }
    public string? Receipt { get; init; }
    public string? Notes { get; init; }
    public required EnvelopeOperationState Status { get; init; }
    public string? VoidReason { get; init; }
    public DateTime? VoidedAt { get; init; }
    public required IReadOnlyList<EnvelopeOperationLineResponse> Lines { get; init; } = [];

    /// <summary>Only right after creating it: the envelopes it left below zero (allowed, RF-PF-08).</summary>
    public required IReadOnlyList<NegativeEnvelopeWarningResponse> Warnings { get; init; } = [];

    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class EnvelopeOperationSummaryResponse
{
    public required Guid Id { get; init; }
    public required int Number { get; init; }
    public required EnvelopeOperationType Kind { get; init; }
    public required DateOnly Date { get; init; }
    public Guid? CashBoxId { get; init; }
    public string? CashBoxName { get; init; }
    public CurrencyCode? Currency { get; init; }
    public Guid? EnvelopeId { get; init; }
    public string? EnvelopeName { get; init; }
    public Guid? ToEnvelopeId { get; init; }
    public string? ToEnvelopeName { get; init; }
    public long? AmountCents { get; init; }
    public string? Concept { get; init; }
    public required int LineCount { get; init; }
    public required EnvelopeOperationState Status { get; init; }
}

public class EnvelopeOperationsResponse : PagedResponse<EnvelopeOperationSummaryResponse>;
