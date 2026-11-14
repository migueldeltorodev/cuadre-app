using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

public class CategoryResponse
{
    public required Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public required string Name { get; init; }
    public string? CodePrefix { get; init; }

    /// <summary>Products in it. For a category this includes the ones filed under its subcategories.</summary>
    public required int ProductCount { get; init; }

    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>Flat list, top-level first; the UI builds the tree with ParentId.</summary>
public class CategoriesResponse
{
    public required IEnumerable<CategoryResponse> Items { get; init; } = [];
}

public class SupplierResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? ContactName { get; init; }
    public string? Phone { get; init; }
    public string? Notes { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class SuppliersResponse
{
    public required IEnumerable<SupplierResponse> Items { get; init; } = [];
}

public class ProductResponse
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public string? Barcode { get; init; }
    public required string Name { get; init; }
    public string? Brand { get; init; }
    public string? Presentation { get; init; }
    public required Guid CategoryId { get; init; }
    public Guid? SubcategoryId { get; init; }
    public Guid? UsualSupplierId { get; init; }
    public required UnitCode PurchaseUnit { get; init; }
    public required UnitCode StockUnit { get; init; }
    public required long ConversionFactorMilli { get; init; }

    /// <summary>Derived from the stock unit (libra yes, unidad no).</summary>
    public required bool AllowsDecimals { get; init; }

    public long? SalePriceCents { get; init; }

    /// <summary>The price differs from the one last copied to the sign.</summary>
    public required bool IsPriceChangedSinceListed { get; init; }

    public required long MinStockMilli { get; init; }
    public required int ShrinkageToleranceBps { get; init; }
    public long? ExchangeBufferCents { get; init; }
    public int? MarkupBps { get; init; }
    public required bool IsPerishable { get; init; }
    public int? ShelfLifeDays { get; init; }
    public required bool IsActive { get; init; }
    public string? Notes { get; init; }

    /// <summary>All locations: SUM of the stock ledger. Can be negative (sold before the purchase was typed).</summary>
    public required long StockMilli { get; init; }

    public DateOnly? LastPurchaseDate { get; init; }

    /// <summary>Null until the first purchase.</summary>
    public ProductCostResponse? Cost { get; init; }

    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class ProductsResponse : PagedResponse<ProductResponse>;

public class NextProductCodeResponse
{
    public required string Code { get; init; }
}

public class PriceChangeResponse
{
    public required Guid Id { get; init; }
    public required long PriceCents { get; init; }
    public long? PreviousPriceCents { get; init; }
    public required DateOnly EffectiveDate { get; init; }
    public required PriceChangeKind Reason { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public class PriceHistoryResponse
{
    public required IEnumerable<PriceChangeResponse> Items { get; init; } = [];
}

public class UnitResponse
{
    public required UnitCode Code { get; init; }
    public required bool AllowsDecimals { get; init; }
}

public class UnitsResponse
{
    public required IEnumerable<UnitResponse> Items { get; init; } = [];
}

public class PriceListEntryResponse
{
    public required Guid ProductId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Brand { get; init; }
    public string? Presentation { get; init; }
    public required UnitCode StockUnit { get; init; }
    public string? SubcategoryName { get; init; }
    public long? SalePriceCents { get; init; }

    /// <summary>The price currently on the sign (null = never copied).</summary>
    public long? ListedPriceCents { get; init; }

    public required bool IsChanged { get; init; }
}

public class PriceListGroupResponse
{
    public required Guid CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required IEnumerable<PriceListEntryResponse> Entries { get; init; } = [];
}

public class PriceListCopyResponse
{
    public required Guid Id { get; init; }
    public required DateTime CopiedAt { get; init; }
    public required int ProductCount { get; init; }
    public required int ChangedCount { get; init; }
}

/// <summary>Grouped by category; subcategory order is kept inside each group.</summary>
public class PriceListResponse
{
    public PriceListCopyResponse? LastCopy { get; init; }
    public required int ChangedCount { get; init; }
    public required IEnumerable<PriceListGroupResponse> Groups { get; init; } = [];
}
