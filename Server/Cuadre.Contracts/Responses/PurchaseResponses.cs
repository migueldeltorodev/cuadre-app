using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

// Unit costs in millionths ("Micros"): 0.62 USD per lb = 620000; CUP cost per lb 201.50 = 201500000.

public class PurchaseLineResponse
{
    public required Guid Id { get; init; }
    public required int LineNo { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required string ProductCode { get; init; }
    public required UnitCode PurchaseUnit { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required long QuantityMilli { get; init; }
    public required long ConversionFactorMilli { get; init; }
    public required long StockQuantityMilli { get; init; }
    public required long UnitCostCents { get; init; }
    public required long LineTotalCents { get; init; }
    public required long AllocatedChargesUsdMicros { get; init; }

    /// <summary>Per STOCK unit, goods + its share of the charges.</summary>
    public required long LandedUnitCostUsdMicros { get; init; }

    public required long LandedUnitCostCupMicros { get; init; }
}

public class PurchaseChargeResponse
{
    public required Guid Id { get; init; }
    public required string Description { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required long AmountCents { get; init; }
    public required Guid CashBoxId { get; init; }
}

public class PriceSuggestionResponse
{
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public long? CurrentPriceCents { get; init; }
    public required long SuggestedPriceCents { get; init; }
}

public class PurchaseResponse
{
    public required Guid Id { get; init; }
    public required int Number { get; init; }
    public required DateOnly Date { get; init; }
    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required long RateCupPerUsdCents { get; init; }
    public required Guid LocationId { get; init; }
    public required Guid CashBoxId { get; init; }
    public required long GoodsTotalCents { get; init; }
    public string? Notes { get; init; }
    public required PurchaseState Status { get; init; }
    public string? VoidReason { get; init; }
    public DateTime? VoidedAt { get; init; }
    public required IEnumerable<PurchaseLineResponse> Lines { get; init; } = [];
    public required IEnumerable<PurchaseChargeResponse> Charges { get; init; } = [];

    /// <summary>Only right after creating it: products whose suggested price changed (RF-CMP-04).</summary>
    public IEnumerable<PriceSuggestionResponse> PriceSuggestions { get; init; } = [];

    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class PurchaseSummaryResponse
{
    public required Guid Id { get; init; }
    public required int Number { get; init; }
    public required DateOnly Date { get; init; }
    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required long GoodsTotalCents { get; init; }
    public required int LineCount { get; init; }
    public required string ProductNames { get; init; }
    public required PurchaseState Status { get; init; }
}

public class PurchasesResponse : PagedResponse<PurchaseSummaryResponse>;

public class ProductPurchaseResponse
{
    public required Guid PurchaseId { get; init; }
    public required int PurchaseNumber { get; init; }
    public required DateOnly Date { get; init; }
    public required string SupplierName { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required long RateCupPerUsdCents { get; init; }
    public required long QuantityMilli { get; init; }
    public required UnitCode PurchaseUnit { get; init; }
    public required long StockQuantityMilli { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required long UnitCostCents { get; init; }
    public required long LandedUnitCostUsdMicros { get; init; }
    public required long LandedUnitCostCupMicros { get; init; }
    public required PurchaseState Status { get; init; }
}

public class ProductPurchasesResponse
{
    public required IEnumerable<ProductPurchaseResponse> Items { get; init; } = [];
}

public class SupplierProductResponse
{
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required string ProductCode { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required DateOnly LastPurchaseDate { get; init; }
    public required long LastLandedUnitCostUsdMicros { get; init; }
    public required int PurchaseCount { get; init; }
}

public class SupplierProductsResponse
{
    public required IEnumerable<SupplierProductResponse> Items { get; init; } = [];
}

public class LocationResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required LocationType Kind { get; init; }
    public required bool IsActive { get; init; }
}

public class LocationsResponse
{
    public required IEnumerable<LocationResponse> Items { get; init; } = [];
}

/// <summary>Cost and price picture of a product. Absent until its first purchase.</summary>
public class ProductCostResponse
{
    /// <summary>Weighted average per stock unit (RN-01): what the stock on the shelf cost.</summary>
    public required long AvgCostUsdMicros { get; init; }

    /// <summary>Historical CUP per stock unit (RN-02).</summary>
    public required long AvgCostCupMicros { get; init; }

    /// <summary>Landed cost per stock unit of the last purchase (D-52): the base of the suggested price.</summary>
    public required long LastCostUsdMicros { get; init; }

    public required long LastCostCupMicros { get; init; }

    /// <summary>The last purchase's rate, CUP cents per USD: the base of the colchón alert (RN-04).</summary>
    public required long LastRateCents { get; init; }

    /// <summary>The colchón and recargo actually used (the product's own or the business default).</summary>
    public required long EffectiveBufferCents { get; init; }

    public required int EffectiveMarkupBps { get; init; }
    public required long SuggestedPriceCents { get; init; }

    /// <summary>Over the sale price, against the last cost; null without a price.</summary>
    public int? MarginBps { get; init; }

    /// <summary>Latest rate on or before today; null when none was ever loaded.</summary>
    public long? TodayRateCents { get; init; }

    /// <summary>RN-04: today's rate went over purchase rate + colchón.</summary>
    public required bool IsBufferExceeded { get; init; }
}
