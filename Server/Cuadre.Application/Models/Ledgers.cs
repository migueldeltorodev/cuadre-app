namespace Cuadre.Application.Models;

/// <summary>
/// One row of the stock ledger (RF-INV-02). Stock = SUM(QuantityMilli): never written by hand.
/// Quantity in thousandths of the product's stock unit; positive = in, negative = out.
/// Incoming rows that carry a cost (purchases) feed the average cost replay (RN-01/RN-02).
/// </summary>
public class StockMovement
{
    public required Guid Id { get; init; }
    public required Guid ProductId { get; init; }
    public required Guid LocationId { get; init; }
    public required DateOnly Date { get; init; }
    public required StockMovementKind Kind { get; init; }
    public required long QuantityMilli { get; init; }
    public long? UnitCostUsdMicros { get; init; }
    public long? UnitCostCupMicros { get; init; }
    public Guid? PurchaseId { get; init; }
    public Guid? PurchaseLineId { get; init; }

    /// <summary>Transfers, exits and counts (slice 4).</summary>
    public Guid? OperationId { get; init; }

    public required DateTime CreatedAt { get; init; }
}

/// <summary>Append only (names are stored): later slices add Sale...</summary>
public enum StockMovementKind
{
    Purchase,
    PurchaseVoid,
    TransferOut,
    TransferIn,
    Exit,
    CountAdjustment,
    OperationVoid
}

/// <summary>
/// One row of the cash ledger. Box balance = SUM(AmountCents) in the box's currency.
/// Negative = money out. Every row belongs to ONE envelope (slice 5), so per box the envelopes add up to the box.
/// </summary>
public class CashMovement
{
    public required Guid Id { get; init; }
    public required Guid CashBoxId { get; init; }

    /// <summary>What the money is for (D-10). Purchases always use Reposición.</summary>
    public required Guid EnvelopeId { get; init; }

    public required DateOnly Date { get; init; }
    public required CashMovementKind Kind { get; init; }
    public required long AmountCents { get; init; }
    public required string Description { get; init; }
    public Guid? PurchaseId { get; init; }

    /// <summary>Opening, move between envelopes, withdrawal, tax payment (slice 5).</summary>
    public Guid? EnvelopeOperationId { get; init; }

    /// <summary>Consumo de la casa (RN-11): the exit that took the goods home.</summary>
    public Guid? StockOperationId { get; init; }

    public required DateTime CreatedAt { get; init; }
}

/// <summary>Append only (names are stored): later slices add Collection, Exchange, Expense...</summary>
public enum CashMovementKind
{
    Purchase,
    PurchaseCharge,
    PurchaseVoid,

    /// <summary>Saldo inicial: sets an envelope to what is physically there (target − balance).</summary>
    Opening,

    /// <summary>Movimiento entre sobres: one row out of an envelope, one into another, same box.</summary>
    EnvelopeTransfer,

    /// <summary>Retiro: money taken out of the business (the owner's pay, the profit).</summary>
    Withdrawal,

    /// <summary>Pago de impuestos, from the Impuestos envelope (RF-IMP-01).</summary>
    TaxPayment,

    /// <summary>Consumo de la casa (RN-11): out of Dueño, into Reposición, at historical cost. No cash moves.</summary>
    HouseConsumption,

    /// <summary>Reversal of an envelope operation.</summary>
    EnvelopeOperationVoid,

    /// <summary>Reversal of the envelope move of a voided house consumption exit.</summary>
    StockOperationVoid
}
