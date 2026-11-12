namespace Cuadre.Application.Models;

/// <summary>
/// A purchase (RF-CMP-01). Saving it confirms it: stock and cash movements are written in the same
/// transaction. It is never edited afterwards, only voided with a reason (RF-CMP-07).
/// Money in cents of <see cref="Currency"/>; the rate converts CUP amounts to USD (RN-01).
/// </summary>
public class Purchase
{
    public required Guid Id { get; init; }
    public int Number { get; set; }
    public required DateOnly Date { get; init; }
    public required Guid SupplierId { get; init; }
    public required Currency Currency { get; init; }
    public required long RateCupPerUsdCents { get; init; }

    /// <summary>Empty = the default Storage location (Almacén).</summary>
    public Guid LocationId { get; set; }

    public required Guid CashBoxId { get; init; }
    public long GoodsTotalCents { get; set; }
    public string? Notes { get; init; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Confirmed;
    public string? VoidReason { get; set; }
    public DateTime? VoidedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<PurchaseLine> Lines { get; init; } = [];
    public List<PurchaseCharge> Charges { get; init; } = [];

    /// <summary>Read-only, filled by queries.</summary>
    public string SupplierName { get; init; } = "";
}

public enum PurchaseStatus
{
    Confirmed,
    Voided
}

public class PurchaseLine
{
    public required Guid Id { get; init; }
    public Guid PurchaseId { get; set; }
    public int LineNo { get; set; }
    public required Guid ProductId { get; init; }

    /// <summary>In PURCHASE units (sacos), thousandths.</summary>
    public required long QuantityMilli { get; init; }

    /// <summary>Per purchase unit, in the purchase currency.</summary>
    public required long UnitCostCents { get; init; }

    /// <summary>Input: null = the product's factor. After calculation: the factor actually used (snapshot).</summary>
    public long? ConversionFactorMilli { get; set; }

    // ---- Computed by PurchaseCalculator (snapshots) ----
    public MeasureUnit PurchaseUnit { get; set; }
    public MeasureUnit StockUnit { get; set; }
    public long StockQuantityMilli { get; set; }
    public long LineTotalCents { get; set; }
    public long AllocatedChargesUsdMicros { get; set; }
    public long LandedUnitCostUsdMicros { get; set; }
    public long LandedUnitCostCupMicros { get; set; }

    /// <summary>Read-only, filled by queries.</summary>
    public string ProductName { get; init; } = "";
    public string ProductCode { get; init; } = "";
}

/// <summary>Freight, unloading... (RF-CMP-02). Each one paid from its own cash box, in its own currency.</summary>
public class PurchaseCharge
{
    public required Guid Id { get; init; }
    public Guid PurchaseId { get; set; }
    public required string Description { get; init; }
    public required Currency Currency { get; init; }
    public required long AmountCents { get; init; }
    public required Guid CashBoxId { get; init; }
}

/// <summary>One row of the purchase history (RF-CMP-06).</summary>
public class PurchaseSummary
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public DateOnly Date { get; init; }
    public Guid SupplierId { get; init; }
    public string SupplierName { get; init; } = "";
    public Currency Currency { get; init; }
    public long GoodsTotalCents { get; init; }
    public int LineCount { get; init; }

    /// <summary>"Arroz guyanés, Azúcar…" (order not guaranteed).</summary>
    public string ProductNames { get; init; } = "";

    public PurchaseStatus Status { get; init; }
}

public class GetAllPurchasesOptions
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? SupplierId { get; init; }
    public Guid? ProductId { get; init; }

    /// <summary>A category or subcategory: purchases with at least one product in it.</summary>
    public Guid? CategoryId { get; init; }

    public bool IncludeVoided { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>A purchase line seen from the product (design "Producto": "Compras de este producto").</summary>
public class ProductPurchase
{
    public Guid PurchaseId { get; init; }
    public int PurchaseNumber { get; init; }
    public DateOnly Date { get; init; }
    public string SupplierName { get; init; } = "";
    public Currency Currency { get; init; }
    public long RateCupPerUsdCents { get; init; }
    public long QuantityMilli { get; init; }
    public MeasureUnit PurchaseUnit { get; init; }
    public long StockQuantityMilli { get; init; }
    public MeasureUnit StockUnit { get; init; }
    public long UnitCostCents { get; init; }
    public long LandedUnitCostUsdMicros { get; init; }
    public long LandedUnitCostCupMicros { get; init; }
    public PurchaseStatus Status { get; init; }
}

/// <summary>What a supplier sells us (RF-PRV-02): each product with its last purchase from them.</summary>
public class SupplierProduct
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = "";
    public string ProductCode { get; init; } = "";
    public MeasureUnit StockUnit { get; init; }
    public DateOnly LastPurchaseDate { get; init; }
    public long LastLandedUnitCostUsdMicros { get; init; }
    public int PurchaseCount { get; init; }
}

/// <summary>After a purchase: products whose suggested price no longer matches their price (RF-CMP-04).</summary>
public sealed record PriceSuggestion(Guid ProductId, string ProductName, long? CurrentPriceCents, long SuggestedPriceCents);

/// <summary>The saved purchase plus the price decisions it opens.</summary>
public sealed record PurchaseResult(Purchase Purchase, IReadOnlyList<PriceSuggestion> PriceSuggestions);
