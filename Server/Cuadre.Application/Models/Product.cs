namespace Cuadre.Application.Models;

/// <summary>
/// A product = a variety/brand (RF-PRD-01, D-05): "arroz guyanés" and "arroz brasileño" are two products;
/// the same rice bought from several suppliers is ONE product.
/// Quantities in thousandths of the STOCK unit (Milli), money in CUP cents, percentages in basis points.
/// Cost, purchase rate and stock arrive with purchases/inventory (slices 3 and 4).
/// </summary>
public class Product
{
    public required Guid Id { get; init; }
    public required string Code { get; set; }
    public string? Barcode { get; set; }
    public required string Name { get; set; }
    public string? Brand { get; set; }
    public string? Presentation { get; set; }

    public required Guid CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public Guid? UsualSupplierId { get; set; }

    /// <summary>How it is bought (saco, caja).</summary>
    public required MeasureUnit PurchaseUnit { get; set; }

    /// <summary>How it is stocked, counted and sold (libra, unidad).</summary>
    public required MeasureUnit StockUnit { get; set; }

    /// <summary>Stock units in one purchase unit, ×1000: 1 sack = 100 lb → 100_000 (RN-13).</summary>
    public required long ConversionFactorMilli { get; set; }

    /// <summary>Null = not priced yet.</summary>
    public long? SalePriceCents { get; set; }

    /// <summary>Price when the price list was last copied to the shop sign (RF-PRD-09). Managed by the price list.</summary>
    public long? ListedPriceCents { get; set; }

    public long MinStockMilli { get; set; }
    public int ShrinkageToleranceBps { get; set; }

    /// <summary>Own colchón / recargo; null = business default.</summary>
    public long? ExchangeBufferCents { get; set; }
    public int? MarkupBps { get; set; }

    public bool IsPerishable { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool AllowsDecimals => StockUnit.AllowsDecimals();
}

public enum PriceChangeReason
{
    Manual,
    Purchase
}

/// <summary>One row of the sale price history (RF-PRD-05). Insert-only.</summary>
public class ProductPriceChange
{
    public required Guid Id { get; init; }
    public required Guid ProductId { get; init; }
    public required long PriceCents { get; init; }
    public long? PreviousPriceCents { get; init; }
    public required DateOnly EffectiveDate { get; init; }
    public required PriceChangeReason Reason { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public class GetAllProductsOptions
{
    /// <summary>Matches code, name, brand or barcode.</summary>
    public string? Search { get; init; }

    /// <summary>A category (includes its subcategories) or a subcategory.</summary>
    public Guid? CategoryId { get; init; }

    public bool IncludeInactive { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
