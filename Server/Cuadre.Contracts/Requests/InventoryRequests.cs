using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Requests;

// Quantities in thousandths (2.5 lb = 2500). A line says whether it was typed in PURCHASE units ("2 sacos")
// or in STOCK units ("50 lb"); the server converts with the product's factor.

public class CreateLocationRequest
{
    public required string Name { get; init; }
    public required LocationType Kind { get; init; }
}

/// <summary>The kind cannot change.</summary>
public class UpdateLocationRequest
{
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}

public class StockOperationLineRequest
{
    public required Guid ProductId { get; init; }

    /// <summary>Transfer/exit: what moves (&gt; 0). Count: what was found (0 allowed).</summary>
    public required long QuantityMilli { get; init; }

    /// <summary>True = in purchase units (sacos); false = in stock units (libras).</summary>
    public bool InPurchaseUnits { get; init; }
}

/// <summary>RF-INV-03: "Llevar al mostrador" is a transfer Almacén → Mostrador.</summary>
public class CreateTransferRequest
{
    public required DateOnly Date { get; init; }
    public required Guid FromLocationId { get; init; }
    public required Guid ToLocationId { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<StockOperationLineRequest> Lines { get; init; } = [];
}

/// <summary>RF-INV-04: goods that leave without being sold.</summary>
public class CreateExitRequest
{
    public required DateOnly Date { get; init; }
    public required Guid LocationId { get; init; }
    public required ExitReasonCode Reason { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<StockOperationLineRequest> Lines { get; init; } = [];
}

/// <summary>RF-INV-08: what was found. The server computes the expected stock and adjusts the difference.</summary>
public class CreateCountRequest
{
    public required DateOnly Date { get; init; }
    public required Guid LocationId { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<StockOperationLineRequest> Lines { get; init; } = [];
}

public class VoidStockOperationRequest
{
    public required string Reason { get; init; }
}

public class GetAllStockOperationsRequest : PagedRequest
{
    public StockOperationType? Kind { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>As origin or destination.</summary>
    public Guid? LocationId { get; init; }

    public Guid? ProductId { get; init; }
    public bool? IncludeVoided { get; init; }
}

public class GetStockRequest
{
    public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? LowOnly { get; init; }
    public bool? IncludeInactive { get; init; }
}

public class GetKardexRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? LocationId { get; init; }
}

public class GetStockMovementsRequest
{
    /// <summary>Null = today.</summary>
    public DateOnly? Date { get; init; }
}
