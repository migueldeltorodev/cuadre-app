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

    /// <summary>Typical shelf life in days (RF-PRD-10). Only for perishables; null = not set.</summary>
    public int? ShelfLifeDays { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool AllowsDecimals => StockUnit.AllowsDecimals();

    // ---- Read-only, filled by queries (stock ledger + product_costs cache). Never written with the product. ----

    /// <summary>Total stock in all locations: SUM of the stock ledger.</summary>
    public long StockMilli { get; init; }

    /// <summary>Null until the first purchase (RN-01).</summary>
    public long? AvgCostUsdMicros { get; init; }

    /// <summary>Historical CUP cost per stock unit (RN-02). Null until the first purchase.</summary>
    public long? AvgCostCupMicros { get; init; }

    /// <summary>Landed cost per stock unit of the last confirmed purchase (D-52). Null until the first purchase.</summary>
    public long? LastCostUsdMicros { get; init; }

    public long? LastCostCupMicros { get; init; }

    /// <summary>D-60: the FIFO batch being sold now (oldest with stock; else the newest). Null until the first purchase.</summary>
    public long? CurrentCostUsdMicros { get; init; }

    public long? CurrentCostCupMicros { get; init; }

    /// <summary>The purchase line of the batch being sold now. Null with an empty shelf or a batch from a count.</summary>
    public Guid? CurrentPurchaseLineId { get; init; }

    /// <summary>The newest purchase line (its batch has the "last cost"). Null if never bought.</summary>
    public Guid? LastPurchaseLineId { get; init; }

    /// <summary>D-61: the batch the current price was decided for.</summary>
    public Guid? PriceReviewedLineId { get; init; }

    /// <summary>D-61: the newest batch whose "it will not fit in the price" warning was dismissed.</summary>
    public Guid? CostlierSeenLineId { get; init; }

    public DateOnly? LastPurchaseDate { get; init; }

    public UnitCost? AverageCost =>
        AvgCostUsdMicros is { } usd && AvgCostCupMicros is { } cup ? new UnitCost(usd, cup) : null;

    public UnitCost? LastCost =>
        LastCostUsdMicros is { } usd && LastCostCupMicros is { } cup ? new UnitCost(usd, cup) : null;

    public UnitCost? CurrentCost =>
        CurrentCostUsdMicros is { } usd && CurrentCostCupMicros is { } cup ? new UnitCost(usd, cup) : null;

    /// <summary>Suggested price, margin, colchón alert. Computed by the service; null without purchases.</summary>
    public ProductPricing? Pricing { get; set; }
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

/// <summary>Accepting a suggested price (RF-CMP-04) or a quick manual change from the product screen.</summary>
public sealed record SetSalePrice(Guid ProductId, long SalePriceCents, Guid? PurchaseId);

/// <summary>D-61: answer a price notice: take the suggested price (from today) or keep the price as it is.</summary>
public sealed record ResolvePriceNotice(Guid ProductId, PriceNoticeKind Kind, bool AcceptSuggested);
