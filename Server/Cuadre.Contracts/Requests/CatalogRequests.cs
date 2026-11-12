using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Requests;

// Quantities in thousandths of the stock unit ("Milli": 2.5 lb = 2500), money in CUP cents,
// percentages in basis points (30 % = 3000).

public class CreateCategoryRequest
{
    public required string Name { get; init; }

    /// <summary>Null = top-level category; set = subcategory of that category.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Top-level only. Blank = derived from the name ("Arroz" → "ARR").</summary>
    public string? CodePrefix { get; init; }
}

/// <summary>The level (parent) cannot change after creation.</summary>
public class UpdateCategoryRequest
{
    public required string Name { get; init; }

    /// <summary>Top-level only. Blank = keep the current one.</summary>
    public string? CodePrefix { get; init; }
}

public class CreateSupplierRequest
{
    public required string Name { get; init; }
    public string? ContactName { get; init; }
    public string? Phone { get; init; }
    public string? Notes { get; init; }
}

public class UpdateSupplierRequest
{
    public required string Name { get; init; }
    public string? ContactName { get; init; }
    public string? Phone { get; init; }
    public string? Notes { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>Fields shared by create and update.</summary>
public abstract class ProductRequestBase
{
    public string? Barcode { get; init; }
    public required string Name { get; init; }
    public string? Brand { get; init; }
    public string? Presentation { get; init; }
    public required Guid CategoryId { get; init; }
    public Guid? SubcategoryId { get; init; }
    public Guid? UsualSupplierId { get; init; }
    public required UnitCode PurchaseUnit { get; init; }
    public required UnitCode StockUnit { get; init; }

    /// <summary>Stock units in one purchase unit ×1000: 1 sack = 100 lb → 100000.</summary>
    public required long ConversionFactorMilli { get; init; }

    /// <summary>Null = not priced yet (only allowed while it never had a price).</summary>
    public long? SalePriceCents { get; init; }

    public long MinStockMilli { get; init; }
    public int ShrinkageToleranceBps { get; init; }

    /// <summary>Null = use the business default.</summary>
    public long? ExchangeBufferCents { get; init; }

    /// <summary>Null = use the business default.</summary>
    public int? MarkupBps { get; init; }

    public bool IsPerishable { get; init; }
    public string? Notes { get; init; }
}

public class CreateProductRequest : ProductRequestBase
{
    /// <summary>Blank = generated from the category prefix (ARR-001...).</summary>
    public string? Code { get; init; }
}

public class UpdateProductRequest : ProductRequestBase
{
    public required string Code { get; init; }
    public required bool IsActive { get; init; }
}

public class GetAllProductsRequest : PagedRequest
{
    /// <summary>Matches code, name, brand or barcode.</summary>
    public string? Search { get; init; }

    /// <summary>A category (includes its subcategories) or a subcategory.</summary>
    public Guid? CategoryId { get; init; }

    public bool? IncludeInactive { get; init; }
}
