namespace Cuadre.Application.Models;

/// <summary>
/// A money document of the envelopes (slice 5): the opening balances, a move between envelopes, a withdrawal or a
/// tax payment. Saving it confirms it: its lines go into the cash ledger in the same transaction. Never edited,
/// only voided with a reason (reversal movements dated the day of the void).
/// </summary>
public class EnvelopeOperation
{
    public required Guid Id { get; init; }
    public int Number { get; set; }
    public required EnvelopeOperationKind Kind { get; init; }
    public required DateOnly Date { get; init; }

    // ---- Single-box kinds (Transfer, Withdrawal, TaxPayment) ----

    public Guid? CashBoxId { get; init; }

    /// <summary>Transfer: origin. Withdrawal: where it leaves from. TaxPayment: resolved to Impuestos.</summary>
    public Guid? EnvelopeId { get; set; }

    /// <summary>Transfer only.</summary>
    public Guid? ToEnvelopeId { get; init; }

    /// <summary>Positive. Single-box kinds only.</summary>
    public long? AmountCents { get; init; }

    /// <summary>TaxPayment: what was paid ("ONAT, noviembre").</summary>
    public string? Concept { get; init; }

    /// <summary>TaxPayment: receipt number.</summary>
    public string? Receipt { get; init; }

    public string? Notes { get; init; }

    /// <summary>Opening only: what is physically in each (box, envelope).</summary>
    public List<OpeningTarget> Targets { get; init; } = [];

    public EnvelopeOperationStatus Status { get; set; } = EnvelopeOperationStatus.Confirmed;
    public string? VoidReason { get; set; }
    public DateTime? VoidedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Resolved by the service (what it did to each box and envelope); read back with names.</summary>
    public List<EnvelopeOperationLine> Lines { get; init; } = [];

    /// <summary>Read-only, filled by queries.</summary>
    public string? CashBoxName { get; init; }

    public Currency? Currency { get; init; }
    public string? EnvelopeName { get; init; }
    public string? ToEnvelopeName { get; init; }
}

/// <summary>Stored by name: append only.</summary>
public enum EnvelopeOperationKind
{
    /// <summary>Saldo inicial (or a later correction): sets each envelope to what is physically there.</summary>
    Opening,

    /// <summary>Movimiento entre sobres, inside one cash box.</summary>
    Transfer,

    /// <summary>Retiro: the owner's pay (RF-DUE-01), the profit...</summary>
    Withdrawal,

    /// <summary>Pago de impuestos (RF-IMP-01), always from Impuestos.</summary>
    TaxPayment
}

public enum EnvelopeOperationStatus
{
    Confirmed,
    Voided
}

/// <summary>Opening input: this much is in this envelope of this box.</summary>
public sealed record OpeningTarget(Guid CashBoxId, Guid EnvelopeId, long TargetCents);

public class EnvelopeOperationLine
{
    public required Guid Id { get; init; }
    public Guid OperationId { get; set; }
    public int LineNo { get; set; }
    public required Guid CashBoxId { get; init; }
    public required Guid EnvelopeId { get; init; }

    /// <summary>Signed, in the box's currency. Opening: target − previous balance (0 = it already matched).</summary>
    public required long AmountCents { get; init; }

    /// <summary>Opening only.</summary>
    public long? TargetCents { get; init; }

    /// <summary>Read-only, filled by queries.</summary>
    public string CashBoxName { get; init; } = "";

    public Currency Currency { get; init; }
    public string EnvelopeName { get; init; } = "";
}

/// <summary>One row of the envelope operations history.</summary>
public class EnvelopeOperationSummary
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public EnvelopeOperationKind Kind { get; init; }
    public DateOnly Date { get; init; }
    public Guid? CashBoxId { get; init; }
    public string? CashBoxName { get; init; }
    public Currency? Currency { get; init; }
    public Guid? EnvelopeId { get; init; }
    public string? EnvelopeName { get; init; }
    public Guid? ToEnvelopeId { get; init; }
    public string? ToEnvelopeName { get; init; }
    public long? AmountCents { get; init; }
    public string? Concept { get; init; }
    public int LineCount { get; init; }
    public EnvelopeOperationStatus Status { get; init; }
}

public class GetAllEnvelopeOperationsOptions
{
    public EnvelopeOperationKind? Kind { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>Operations touching this envelope (any line).</summary>
    public Guid? EnvelopeId { get; init; }

    public bool IncludeVoided { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed record VoidEnvelopeOperation(Guid OperationId, string Reason);

/// <summary>Allowed (RF-PF-08) but shown: after this operation the envelope holds less than nothing in that box.</summary>
public sealed record NegativeEnvelopeWarning(
    Guid EnvelopeId,
    string EnvelopeName,
    Guid CashBoxId,
    string CashBoxName,
    Currency Currency,
    long BalanceCents);

public sealed record EnvelopeOperationResult(EnvelopeOperation Operation, IReadOnlyList<NegativeEnvelopeWarning> Warnings);
