namespace Cuadre.Application.Models;

/// <summary>
/// A stock document that is not a purchase (slice 4): a transfer between locations (RF-INV-03), a non-sale exit
/// (RF-INV-04) or a physical count (RF-INV-08). Saving it confirms it: its movements go into the stock ledger in
/// the same transaction. Never edited afterwards, only voided with a reason (reversal movements).
/// </summary>
public class StockOperation
{
    public required Guid Id { get; init; }
    public int Number { get; set; }
    public required StockOperationKind Kind { get; init; }
    public required DateOnly Date { get; init; }

    /// <summary>Transfer: origin. Exit: where the goods left from. Count: the location counted.</summary>
    public required Guid LocationId { get; init; }

    /// <summary>Transfer only.</summary>
    public Guid? ToLocationId { get; init; }

    /// <summary>Exit only.</summary>
    public ExitReason? ExitReason { get; init; }

    public string? Notes { get; init; }
    public StockOperationStatus Status { get; set; } = StockOperationStatus.Confirmed;
    public string? VoidReason { get; set; }
    public DateTime? VoidedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<StockOperationLine> Lines { get; init; } = [];

    /// <summary>Read-only, filled by queries.</summary>
    public string LocationName { get; init; } = "";

    public string? ToLocationName { get; init; }
}

/// <summary>Stored by name: append only.</summary>
public enum StockOperationKind
{
    Transfer,
    Exit,
    Count
}

public enum StockOperationStatus
{
    Confirmed,
    Voided
}

/// <summary>Why goods left without being sold (RF-INV-04). Stored by name: append only.</summary>
public enum ExitReason
{
    /// <summary>Merma (RN-06b): a direct loss.</summary>
    Shrinkage,

    /// <summary>Vencido: a direct loss.</summary>
    Expired,

    /// <summary>Roto o dañado: a direct loss.</summary>
    Damaged,

    /// <summary>Consumo de la casa (RN-11): an owner's withdrawal in kind. The envelope move comes in slice 5.</summary>
    HouseConsumption,

    /// <summary>Ajuste: a correction that is not a loss (typing error found later, etc.).</summary>
    Adjustment
}

public class StockOperationLine
{
    public required Guid Id { get; init; }
    public Guid OperationId { get; set; }
    public int LineNo { get; set; }
    public required Guid ProductId { get; init; }

    /// <summary>As typed. Transfer/Exit: the quantity moved. Count: the quantity counted (may be 0).</summary>
    public required long EnteredQuantityMilli { get; init; }

    /// <summary>True = typed in purchase units ("2 sacos"); converted with the product's factor.</summary>
    public required bool EnteredInPurchaseUnits { get; init; }

    // ---- Resolved by the service (snapshots) ----

    /// <summary>The factor used to convert, even when typed in stock units (then it is just recorded).</summary>
    public long ConversionFactorMilli { get; set; }

    /// <summary>Stock units. Transfer/Exit: moved (&gt; 0). Count: counted − expected (0 = it matched).</summary>
    public long QuantityMilli { get; set; }

    /// <summary>Count only: what the ledger said was there.</summary>
    public long? ExpectedMilli { get; set; }

    /// <summary>Count only: what was found, in stock units.</summary>
    public long? CountedMilli { get; set; }

    /// <summary>Average cost per stock unit at that moment (exits and count differences). Null if never bought.</summary>
    public long? UnitCostUsdMicros { get; set; }

    public long? UnitCostCupMicros { get; set; }

    /// <summary>Read-only, filled by queries.</summary>
    public string ProductName { get; init; } = "";

    public string ProductCode { get; init; } = "";
    public MeasureUnit PurchaseUnit { get; init; }
    public MeasureUnit StockUnit { get; init; }

    /// <summary>Historical CUP value of the quantity (exits: what was lost or taken home). Null without cost.</summary>
    public long? ValueCupCents => UnitCostCupMicros is { } cost
        ? Units.RoundToLong(QuantityMilli * (decimal)cost / Units.MilliPerUnit / 10_000m)
        : null;
}

/// <summary>One row of the operations history.</summary>
public class StockOperationSummary
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public StockOperationKind Kind { get; init; }
    public DateOnly Date { get; init; }
    public Guid LocationId { get; init; }
    public string LocationName { get; init; } = "";
    public Guid? ToLocationId { get; init; }
    public string? ToLocationName { get; init; }
    public ExitReason? ExitReason { get; init; }
    public int LineCount { get; init; }

    /// <summary>"Arroz guyanés, Azúcar…" (order not guaranteed).</summary>
    public string ProductNames { get; init; } = "";

    /// <summary>Exits: value of what left. Counts: value of the shortage minus the surplus (positive = loss).</summary>
    public long? ValueCupCents { get; init; }

    public StockOperationStatus Status { get; init; }
}

public class GetAllStockOperationsOptions
{
    public StockOperationKind? Kind { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>Operations touching this location (as origin or destination).</summary>
    public Guid? LocationId { get; init; }

    public Guid? ProductId { get; init; }
    public bool IncludeVoided { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>Input of a void: the reason is mandatory.</summary>
public sealed record VoidStockOperation(Guid OperationId, string Reason);

/// <summary>
/// Allowed (decision 2026-11-12) but shown: after this operation the location holds less than nothing, which
/// usually means a purchase or a transfer was not typed in yet.
/// </summary>
public sealed record NegativeStockWarning(
    Guid ProductId,
    string ProductName,
    MeasureUnit StockUnit,
    Guid LocationId,
    string LocationName,
    long BalanceMilli);

/// <summary>The saved operation plus the locations it left negative.</summary>
public sealed record StockOperationResult(StockOperation Operation, IReadOnlyList<NegativeStockWarning> Warnings);
