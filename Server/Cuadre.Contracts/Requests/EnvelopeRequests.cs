using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Requests;

// Money in cents of the CASH BOX's currency; percentages in basis points (30% = 3000).

/// <summary>Only the user's own envelopes are created; the Profit First ones are seeded.</summary>
public class CreateEnvelopeRequest
{
    public required string Name { get; init; }
}

/// <summary>The role cannot change.</summary>
public class UpdateEnvelopeRequest
{
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}

public class AllocationPlanItemRequest
{
    public required Guid EnvelopeId { get; init; }
    public required int PercentBps { get; init; }
}

/// <summary>New % from a date on (RF-PF-05). Envelopes left out get 0%. They must add up to 100%.</summary>
public class CreateAllocationPlanRequest
{
    public required DateOnly EffectiveFrom { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<AllocationPlanItemRequest> Items { get; init; } = [];
}

public class GetEnvelopeOverviewRequest
{
    /// <summary>Balances at the end of this day. Default: today.</summary>
    public DateOnly? Date { get; init; }
}

public class GetEnvelopeLedgerRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? CashBoxId { get; init; }
}

public class OpeningTargetRequest
{
    public required Guid CashBoxId { get; init; }
    public required Guid EnvelopeId { get; init; }

    /// <summary>What is physically in the envelope (≥ 0). The server moves the difference.</summary>
    public required long AmountCents { get; init; }
}

/// <summary>Saldo inicial (or a later correction): sets each listed envelope of each box to the amount.</summary>
public class CreateEnvelopeOpeningRequest
{
    public required DateOnly Date { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<OpeningTargetRequest> Targets { get; init; } = [];
}

/// <summary>Movimiento entre sobres, inside one cash box (RF-PF-07).</summary>
public class CreateEnvelopeTransferRequest
{
    public required DateOnly Date { get; init; }
    public required Guid CashBoxId { get; init; }
    public required Guid FromEnvelopeId { get; init; }
    public required Guid ToEnvelopeId { get; init; }
    public required long AmountCents { get; init; }
    public string? Notes { get; init; }
}

/// <summary>Retiro (RF-DUE-01): usually the owner's pay or the profit.</summary>
public class CreateEnvelopeWithdrawalRequest
{
    public required DateOnly Date { get; init; }
    public required Guid CashBoxId { get; init; }
    public required Guid EnvelopeId { get; init; }
    public required long AmountCents { get; init; }
    public string? Notes { get; init; }
}

/// <summary>Pago de impuestos (RF-IMP-01): always from the Impuestos envelope.</summary>
public class CreateTaxPaymentRequest
{
    public required DateOnly Date { get; init; }
    public required Guid CashBoxId { get; init; }
    public required long AmountCents { get; init; }
    public required string Concept { get; init; }
    public string? Receipt { get; init; }
    public string? Notes { get; init; }
}

public class VoidEnvelopeOperationRequest
{
    public required string Reason { get; init; }
}

public class GetAllEnvelopeOperationsRequest : PagedRequest
{
    public EnvelopeOperationType? Kind { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? EnvelopeId { get; init; }
    public bool? IncludeVoided { get; init; }
}
