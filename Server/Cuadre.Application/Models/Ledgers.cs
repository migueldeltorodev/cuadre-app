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
    public required DateTime CreatedAt { get; init; }
}

/// <summary>Append only (names are stored): slice 4 adds Transfer, Exit, Count...</summary>
public enum StockMovementKind
{
    Purchase,
    PurchaseVoid
}

/// <summary>
/// One row of the cash ledger. Box balance = SUM(AmountCents) in the box's currency.
/// Negative = money out. Envelopes (slice 5) will be attached to these rows.
/// </summary>
public class CashMovement
{
    public required Guid Id { get; init; }
    public required Guid CashBoxId { get; init; }
    public required DateOnly Date { get; init; }
    public required CashMovementKind Kind { get; init; }
    public required long AmountCents { get; init; }
    public required string Description { get; init; }
    public Guid? PurchaseId { get; init; }
    public required DateTime CreatedAt { get; init; }
}

/// <summary>Append only (names are stored): later slices add Collection, Exchange, Expense...</summary>
public enum CashMovementKind
{
    Purchase,
    PurchaseCharge,
    PurchaseVoid
}
