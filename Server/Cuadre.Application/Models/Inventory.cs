namespace Cuadre.Application.Models;

// Read models of the Inventario screen. Everything here is DERIVED from the stock ledger (RF-INV-02).

/// <summary>A location as a column of the stock table, with its last count (the "Contar el almacén" reminder).</summary>
public class StockLocationColumn
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public LocationKind Kind { get; init; }
    public DateOnly? LastCountDate { get; init; }
}

/// <summary>Stock of one product at one location.</summary>
public class LocationQuantity
{
    public Guid LocationId { get; init; }
    public long QuantityMilli { get; init; }
}

/// <summary>A row of the stock table: one product, its quantity per location and its total.</summary>
public class ProductStock
{
    public Guid ProductId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public Guid CategoryId { get; init; }
    public MeasureUnit PurchaseUnit { get; init; }
    public MeasureUnit StockUnit { get; init; }
    public long ConversionFactorMilli { get; init; }
    public long MinStockMilli { get; init; }
    public bool IsActive { get; init; }
    public DateOnly? LastPurchaseDate { get; init; }
    public List<LocationQuantity> Locations { get; init; } = [];
    public long TotalMilli => Locations.Sum(location => location.QuantityMilli);

    /// <summary>
    /// RF-INV-05: total below the minimum. Only once bought: a product never purchased is "not stocked yet",
    /// not "low" (same rule as the product list).
    /// </summary>
    public bool IsLow => LastPurchaseDate is not null && MinStockMilli > 0 && TotalMilli < MinStockMilli;
}

public class GetStockOptions
{
    /// <summary>Matches code or name.</summary>
    public string? Search { get; init; }

    /// <summary>A category (includes its subcategories) or a subcategory.</summary>
    public Guid? CategoryId { get; init; }

    public bool LowOnly { get; init; }
    public bool IncludeInactive { get; init; }
}

public sealed record StockOverview(IReadOnlyList<StockLocationColumn> Locations, IReadOnlyList<ProductStock> Products);

/// <summary>
/// A stock movement with the document it belongs to: the rows of "Movimientos de hoy" and of the kardex.
/// </summary>
public class StockMovementEntry
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = "";
    public MeasureUnit StockUnit { get; init; }
    public Guid LocationId { get; init; }
    public string LocationName { get; init; } = "";
    public DateOnly Date { get; init; }
    public StockMovementKind Kind { get; init; }
    public long QuantityMilli { get; init; }
    public long? UnitCostUsdMicros { get; init; }
    public long? UnitCostCupMicros { get; init; }
    public Guid? PurchaseId { get; init; }
    public int? PurchaseNumber { get; init; }
    public Guid? OperationId { get; init; }
    public int? OperationNumber { get; init; }
    public ExitReason? ExitReason { get; init; }

    /// <summary>The purchase or operation was voided later (its row and its reversal both stay).</summary>
    public bool IsVoided { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>A kardex row (RF-INV-07): the movement plus the balances right after it.</summary>
public sealed record KardexEntry(StockMovementEntry Movement, long LocationBalanceMilli, long TotalBalanceMilli);

/// <summary>
/// The kardex of one product: the balances before <c>From</c> (opening) and the rows in the period, oldest first.
/// </summary>
public sealed record Kardex(
    Product Product,
    IReadOnlyList<LocationQuantity> OpeningBalances,
    IReadOnlyList<KardexEntry> Entries,
    IReadOnlyList<LocationQuantity> ClosingBalances);

public class GetKardexOptions
{
    public required Guid ProductId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>Only rows of this location (balances are still computed per location).</summary>
    public Guid? LocationId { get; init; }
}

/// <summary>A suggested purchase line (RF-INV-06): a low product and how much brings it back to the minimum.</summary>
public class ShoppingListItem
{
    public Guid ProductId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public MeasureUnit PurchaseUnit { get; init; }
    public MeasureUnit StockUnit { get; init; }
    public long ConversionFactorMilli { get; init; }
    public long StockMilli { get; init; }
    public long MinStockMilli { get; init; }
    public Guid? UsualSupplierId { get; init; }
    public string? UsualSupplierName { get; init; }

    /// <summary>Landed cost per stock unit of the last confirmed purchase. Null if never bought.</summary>
    public long? LastUnitCostUsdMicros { get; init; }

    /// <summary>What is missing to reach the minimum, in stock units.</summary>
    public long MissingMilli => Math.Max(0, MinStockMilli - StockMilli);

    /// <summary>The missing quantity rounded UP to whole purchase units (you buy whole sacks).</summary>
    public long SuggestedPurchaseQuantityMilli => InventoryMath.WholePurchaseUnitsFor(MissingMilli, ConversionFactorMilli);

    /// <summary>Rough USD estimate of the suggested quantity at the last cost, in cents. Null if never bought.</summary>
    public long? EstimatedUsdCents => LastUnitCostUsdMicros is { } cost
        ? Units.RoundToLong(SuggestedPurchaseQuantityMilli * (decimal)ConversionFactorMilli / Units.MilliPerUnit
                            * cost / Units.MilliPerUnit / 10_000m)
        : null;
}

/// <summary>Pure unit math of slice 4. Unit tested.</summary>
public static class InventoryMath
{
    /// <summary>
    /// Converts what was typed into stock-unit thousandths. In purchase units: × factor (null when not exact).
    /// In stock units: as is.
    /// </summary>
    public static long? ToStockMilli(long enteredMilli, bool inPurchaseUnits, long conversionFactorMilli) =>
        inPurchaseUnits ? PurchaseCalculator.StockQuantityMilli(enteredMilli, conversionFactorMilli) : enteredMilli;

    /// <summary>Stock thousandths → whole purchase units, rounded UP, as thousandths: 150 lb in 100 lb sacks → 2000.</summary>
    public static long WholePurchaseUnitsFor(long stockMilli, long conversionFactorMilli)
    {
        if (stockMilli <= 0 || conversionFactorMilli <= 0)
        {
            return 0;
        }

        var units = (long)decimal.Ceiling((decimal)stockMilli / conversionFactorMilli);
        return units * Units.MilliPerUnit;
    }

    /// <summary>
    /// The kardex: walks the movements (already in ledger order) keeping a balance per location and a total.
    /// </summary>
    public static IReadOnlyList<KardexEntry> RunningBalances(IEnumerable<StockMovementEntry> movements,
        IDictionary<Guid, long> balances)
    {
        var total = balances.Values.Sum();
        var entries = new List<KardexEntry>();

        foreach (var movement in movements)
        {
            balances.TryGetValue(movement.LocationId, out var atLocation);
            atLocation += movement.QuantityMilli;
            balances[movement.LocationId] = atLocation;
            total += movement.QuantityMilli;
            entries.Add(new KardexEntry(movement, atLocation, total));
        }

        return entries;
    }
}
