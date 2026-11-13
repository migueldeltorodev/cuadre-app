using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>Read side of inventory: every number here is a SUM over the stock ledger (RF-INV-02).</summary>
public interface IInventoryRepository
{
    /// <summary>
    /// Stock of each product at one location (missing = 0). With <paramref name="onOrBefore"/>, only movements
    /// dated up to that day: what a count of that day should find.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, long>> GetBalancesAsync(Guid locationId, IEnumerable<Guid> productIds,
        DateOnly? onOrBefore = null, CancellationToken cancellationToken = default);

    /// <summary>Active locations, plus inactive ones that still hold stock. Counter first.</summary>
    Task<IEnumerable<StockLocationColumn>> GetStockLocationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Products (filtered) with their stock per location. LowOnly is applied by the service.</summary>
    Task<IEnumerable<ProductStock>> GetProductStocksAsync(GetStockOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>Every movement of one day (all products), in ledger order.</summary>
    Task<IEnumerable<StockMovementEntry>> GetMovementsOfDayAsync(DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>Every movement of one product, in ledger order (the kardex walks them).</summary>
    Task<IEnumerable<StockMovementEntry>> GetProductMovementsAsync(Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>Active, already-bought products below their minimum, grouped by usual supplier (RF-INV-06).</summary>
    Task<IEnumerable<ShoppingListItem>> GetShoppingListAsync(CancellationToken cancellationToken = default);
}

public class InventoryRepository : IInventoryRepository
{
    // A movement with its document. "Voided" = its purchase or operation was voided later, or its closing reopened.
    private const string MovementSelect = """
        SELECT m.id, m.product_id, pr.name AS product_name, pr.stock_unit, m.location_id, l.name AS location_name,
               m.date, m.kind, m.quantity_milli, m.unit_cost_usd_micros, m.unit_cost_cup_micros,
               m.purchase_id, p.number AS purchase_number, m.operation_id, o.number AS operation_number,
               o.exit_reason, m.closing_id, c.date AS closing_date,
               (COALESCE(p.status, o.status, c.status, 'Confirmed') = 'Voided') AS is_voided, m.created_at
        FROM stock_movements m
            JOIN products pr ON pr.id = m.product_id
            JOIN locations l ON l.id = m.location_id
            LEFT JOIN purchases p ON p.id = m.purchase_id
            LEFT JOIN stock_operations o ON o.id = m.operation_id
            LEFT JOIN daily_closings c ON c.id = m.closing_id
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public InventoryRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<IReadOnlyDictionary<Guid, long>> GetBalancesAsync(Guid locationId, IEnumerable<Guid> productIds,
        DateOnly? onOrBefore = null, CancellationToken cancellationToken = default)
    {
        // Ids go as text: Dapper's IN-list expansion does not run our Guid handler on each element.
        var ids = productIds.Distinct().Select(id => id.ToString("D")).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, long>();
        }

        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LocationQuantityRow>(new CommandDefinition("""
            SELECT product_id, SUM(quantity_milli) AS quantity_milli
            FROM stock_movements
            WHERE location_id = @locationId
              AND product_id IN @ids
              AND (@onOrBefore IS NULL OR date <= @onOrBefore)
            GROUP BY product_id
            """, new { locationId, ids, onOrBefore }, cancellationToken: cancellationToken));

        return rows.ToDictionary(row => row.ProductId, row => row.QuantityMilli);
    }

    public async Task<IEnumerable<StockLocationColumn>> GetStockLocationsAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<StockLocationColumn>(new CommandDefinition("""
            SELECT l.id, l.name, l.kind,
                   (SELECT MAX(o.date) FROM stock_operations o
                    WHERE o.location_id = l.id AND o.kind = 'Count' AND o.status = 'Confirmed') AS last_count_date
            FROM locations l
            WHERE l.is_active = 1
               OR EXISTS (SELECT 1 FROM stock_movements m WHERE m.location_id = l.id
                          GROUP BY m.product_id HAVING SUM(m.quantity_milli) <> 0)
            ORDER BY CASE l.kind WHEN 'Counter' THEN 0 ELSE 1 END, l.created_at
            """, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<ProductStock>> GetProductStocksAsync(GetStockOptions options,
        CancellationToken cancellationToken = default)
    {
        var search = string.IsNullOrWhiteSpace(options.Search) ? null : options.Search.Trim();

        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var products = (await connection.QueryAsync<ProductStock>(new CommandDefinition("""
            SELECT p.id AS product_id, p.code, p.name, p.category_id, p.purchase_unit, p.stock_unit,
                   p.conversion_factor_milli, p.min_stock_milli, p.is_active, c.last_purchase_date
            FROM products p
                LEFT JOIN product_costs c ON c.product_id = p.id
            WHERE (@search IS NULL OR p.code LIKE @pattern ESCAPE '\' OR p.name LIKE @pattern ESCAPE '\')
              AND (@categoryId IS NULL OR p.category_id = @categoryId OR p.subcategory_id = @categoryId)
              AND (@includeInactive = 1 OR p.is_active = 1)
            ORDER BY p.name COLLATE NOCASE, p.code
            """, new
        {
            search,
            pattern = search is null ? null : $"%{EscapeLike(search)}%",
            categoryId = options.CategoryId,
            includeInactive = options.IncludeInactive
        }, cancellationToken: cancellationToken))).ToList();

        // One pass over the ledger for everything: a few thousand rows at most for a shop.
        var balances = await connection.QueryAsync<LocationQuantityRow>(new CommandDefinition("""
            SELECT product_id, location_id, SUM(quantity_milli) AS quantity_milli
            FROM stock_movements
            GROUP BY product_id, location_id
            HAVING SUM(quantity_milli) <> 0
            """, cancellationToken: cancellationToken));

        var byProduct = balances.ToLookup(row => row.ProductId);
        foreach (var product in products)
        {
            product.Locations.AddRange(byProduct[product.ProductId].Select(row => new LocationQuantity
            {
                LocationId = row.LocationId,
                QuantityMilli = row.QuantityMilli
            }));
        }

        return products;
    }

    public async Task<IEnumerable<StockMovementEntry>> GetMovementsOfDayAsync(DateOnly date,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<StockMovementEntry>(new CommandDefinition($"""
            {MovementSelect}
            WHERE m.date = @date
            ORDER BY m.created_at, m.rowid
            """, new { date }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<StockMovementEntry>> GetProductMovementsAsync(Guid productId,
        CancellationToken cancellationToken = default)
    {
        // Same order as the cost replay: date, then when it was typed in.
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<StockMovementEntry>(new CommandDefinition($"""
            {MovementSelect}
            WHERE m.product_id = @productId
            ORDER BY m.date, m.created_at, m.rowid
            """, new { productId }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<ShoppingListItem>> GetShoppingListAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<ShoppingListItem>(new CommandDefinition("""
            WITH stock AS (
                SELECT product_id, SUM(quantity_milli) AS stock_milli
                FROM stock_movements
                GROUP BY product_id
            ),
            last_cost AS (
                SELECT l.product_id, l.landed_unit_cost_usd_micros,
                       ROW_NUMBER() OVER (PARTITION BY l.product_id ORDER BY p.date DESC, p.number DESC) AS rn
                FROM purchase_lines l
                    JOIN purchases p ON p.id = l.purchase_id
                WHERE p.status = 'Confirmed'
            )
            SELECT pr.id AS product_id, pr.code, pr.name, pr.purchase_unit, pr.stock_unit, pr.conversion_factor_milli,
                   COALESCE(s.stock_milli, 0) AS stock_milli, pr.min_stock_milli, pr.usual_supplier_id,
                   su.name AS usual_supplier_name, lc.landed_unit_cost_usd_micros AS last_unit_cost_usd_micros
            FROM products pr
                JOIN product_costs pc ON pc.product_id = pr.id AND pc.last_purchase_date IS NOT NULL
                LEFT JOIN stock s ON s.product_id = pr.id
                LEFT JOIN suppliers su ON su.id = pr.usual_supplier_id
                LEFT JOIN last_cost lc ON lc.product_id = pr.id AND lc.rn = 1
            WHERE pr.is_active = 1
              AND pr.min_stock_milli > 0
              AND COALESCE(s.stock_milli, 0) < pr.min_stock_milli
            ORDER BY su.name IS NULL, su.name COLLATE NOCASE, pr.name COLLATE NOCASE
            """, cancellationToken: cancellationToken));
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    private sealed class LocationQuantityRow
    {
        public Guid ProductId { get; init; }
        public Guid LocationId { get; init; }
        public long QuantityMilli { get; init; }
    }
}
