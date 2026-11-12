using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

// Quantities in thousandths of the product's STOCK unit; unit costs in millionths; money in CUP cents.

public class StockOperationLineResponse
{
    public required Guid Id { get; init; }
    public required int LineNo { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required string ProductCode { get; init; }
    public required UnitCode PurchaseUnit { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required long EnteredQuantityMilli { get; init; }
    public required bool EnteredInPurchaseUnits { get; init; }
    public required long ConversionFactorMilli { get; init; }

    /// <summary>Transfer/exit: moved. Count: counted − expected (negative = shortage).</summary>
    public required long QuantityMilli { get; init; }

    public long? ExpectedMilli { get; init; }
    public long? CountedMilli { get; init; }
    public long? UnitCostUsdMicros { get; init; }
    public long? UnitCostCupMicros { get; init; }

    /// <summary>Historical CUP value of QuantityMilli. Null without cost.</summary>
    public long? ValueCupCents { get; init; }
}

public class NegativeStockWarningResponse
{
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required Guid LocationId { get; init; }
    public required string LocationName { get; init; }
    public required long BalanceMilli { get; init; }
}

public class StockOperationResponse
{
    public required Guid Id { get; init; }
    public required int Number { get; init; }
    public required StockOperationType Kind { get; init; }
    public required DateOnly Date { get; init; }
    public required Guid LocationId { get; init; }
    public required string LocationName { get; init; }
    public Guid? ToLocationId { get; init; }
    public string? ToLocationName { get; init; }
    public ExitReasonCode? ExitReason { get; init; }
    public string? Notes { get; init; }
    public required StockOperationState Status { get; init; }
    public string? VoidReason { get; init; }
    public DateTime? VoidedAt { get; init; }
    public required IEnumerable<StockOperationLineResponse> Lines { get; init; } = [];

    /// <summary>Only right after creating it: locations left below zero (allowed, but worth a look).</summary>
    public IEnumerable<NegativeStockWarningResponse> Warnings { get; init; } = [];

    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class StockOperationSummaryResponse
{
    public required Guid Id { get; init; }
    public required int Number { get; init; }
    public required StockOperationType Kind { get; init; }
    public required DateOnly Date { get; init; }
    public required Guid LocationId { get; init; }
    public required string LocationName { get; init; }
    public Guid? ToLocationId { get; init; }
    public string? ToLocationName { get; init; }
    public ExitReasonCode? ExitReason { get; init; }
    public required int LineCount { get; init; }
    public required string ProductNames { get; init; }

    /// <summary>Exit: value of what left. Count: shortage minus surplus (positive = loss). Null for transfers.</summary>
    public long? ValueCupCents { get; init; }

    public required StockOperationState Status { get; init; }
}

public class StockOperationsResponse : PagedResponse<StockOperationSummaryResponse>;

public class StockLocationResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required LocationType Kind { get; init; }

    /// <summary>Last confirmed count of this location; null if never counted.</summary>
    public DateOnly? LastCountDate { get; init; }
}

public class LocationQuantityResponse
{
    public required Guid LocationId { get; init; }
    public required long QuantityMilli { get; init; }
}

public class ProductStockResponse
{
    public required Guid ProductId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required Guid CategoryId { get; init; }
    public required UnitCode PurchaseUnit { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required long ConversionFactorMilli { get; init; }
    public required long MinStockMilli { get; init; }
    public required bool IsActive { get; init; }

    /// <summary>Only locations with a non-zero balance.</summary>
    public required IEnumerable<LocationQuantityResponse> Locations { get; init; } = [];

    public required long TotalMilli { get; init; }
    public required bool IsLow { get; init; }
}

public class StockResponse
{
    /// <summary>The columns of the table, Counter first.</summary>
    public required IEnumerable<StockLocationResponse> Locations { get; init; } = [];

    public required IEnumerable<ProductStockResponse> Items { get; init; } = [];
}

public class StockMovementResponse
{
    public required Guid Id { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required Guid LocationId { get; init; }
    public required string LocationName { get; init; }
    public required DateOnly Date { get; init; }
    public required StockMovementType Kind { get; init; }
    public required long QuantityMilli { get; init; }
    public long? UnitCostUsdMicros { get; init; }
    public long? UnitCostCupMicros { get; init; }
    public Guid? PurchaseId { get; init; }
    public int? PurchaseNumber { get; init; }
    public Guid? OperationId { get; init; }
    public int? OperationNumber { get; init; }
    public ExitReasonCode? ExitReason { get; init; }

    /// <summary>Its document was voided later. The row and its reversal both stay in the history.</summary>
    public required bool IsVoided { get; init; }

    public required DateTime CreatedAt { get; init; }
}

public class StockMovementsResponse
{
    public required DateOnly Date { get; init; }
    public required IEnumerable<StockMovementResponse> Items { get; init; } = [];
}

public class KardexEntryResponse
{
    public required StockMovementResponse Movement { get; init; }

    /// <summary>Balance of the movement's location right after it.</summary>
    public required long LocationBalanceMilli { get; init; }

    /// <summary>Balance of all locations right after it.</summary>
    public required long TotalBalanceMilli { get; init; }
}

public class KardexResponse
{
    public required Guid ProductId { get; init; }
    public required string ProductCode { get; init; }
    public required string ProductName { get; init; }
    public required UnitCode PurchaseUnit { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required long ConversionFactorMilli { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>Balances before From (empty without From).</summary>
    public required IEnumerable<LocationQuantityResponse> OpeningBalances { get; init; } = [];

    /// <summary>Oldest first.</summary>
    public required IEnumerable<KardexEntryResponse> Entries { get; init; } = [];

    public required IEnumerable<LocationQuantityResponse> ClosingBalances { get; init; } = [];
}

public class ShoppingListItemResponse
{
    public required Guid ProductId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required UnitCode PurchaseUnit { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required long ConversionFactorMilli { get; init; }
    public required long StockMilli { get; init; }
    public required long MinStockMilli { get; init; }
    public required long MissingMilli { get; init; }

    /// <summary>In PURCHASE units, rounded up to whole ones.</summary>
    public required long SuggestedPurchaseQuantityMilli { get; init; }

    public Guid? UsualSupplierId { get; init; }
    public string? UsualSupplierName { get; init; }
    public long? LastUnitCostUsdMicros { get; init; }

    /// <summary>Rough estimate at the last landed cost, USD cents.</summary>
    public long? EstimatedUsdCents { get; init; }
}

public class ShoppingListResponse
{
    public required IEnumerable<ShoppingListItemResponse> Items { get; init; } = [];
}
