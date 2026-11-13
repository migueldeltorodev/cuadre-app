using System.Data;
using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IPurchaseRepository
{
    /// <summary>MAX + 1. Single local user, and number is UNIQUE anyway: a clash fails instead of duplicating.</summary>
    Task<int> GetNextNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a purchase in ONE transaction: header, lines, charges, stock and cash movements,
    /// and the recalculated average cost of every product in it.
    /// </summary>
    Task CreateAsync(Purchase purchase, IReadOnlyList<StockMovement> stockMovements,
        IReadOnlyList<CashMovement> cashMovements, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>With supplier name, lines (with product name and code) and charges.</summary>
    Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<PurchaseSummary>> GetAllAsync(GetAllPurchasesOptions options,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllPurchasesOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks it voided and writes the reversal movements, then replays the costs of its products.
    /// One transaction, like the creation.
    /// </summary>
    Task VoidAsync(Purchase purchase, IReadOnlyList<StockMovement> stockMovements,
        IReadOnlyList<CashMovement> cashMovements, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>The lines of a product across purchases, newest first.</summary>
    Task<IEnumerable<ProductPurchase>> GetProductPurchasesAsync(Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>Products bought from a supplier with their last landed cost (RF-PRV-02). Confirmed purchases only.</summary>
    Task<IEnumerable<SupplierProduct>> GetSupplierProductsAsync(Guid supplierId,
        CancellationToken cancellationToken = default);
}

public class PurchaseRepository : IPurchaseRepository
{
    // Shared by GetAll and GetCount so the total always matches the page.
    private const string ListFilter = """
        (@from IS NULL OR p.date >= @from)
        AND (@to IS NULL OR p.date <= @to)
        AND (@supplierId IS NULL OR p.supplier_id = @supplierId)
        AND (@includeVoided = 1 OR p.status = 'Confirmed')
        AND (@productId IS NULL OR EXISTS (
                SELECT 1 FROM purchase_lines fl WHERE fl.purchase_id = p.id AND fl.product_id = @productId))
        AND (@categoryId IS NULL OR EXISTS (
                SELECT 1 FROM purchase_lines fl JOIN products fp ON fp.id = fl.product_id
                WHERE fl.purchase_id = p.id AND (fp.category_id = @categoryId OR fp.subcategory_id = @categoryId)))
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public PurchaseRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<int> GetNextNumberAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COALESCE(MAX(number), 0) + 1 FROM purchases", cancellationToken: cancellationToken));
    }

    public async Task CreateAsync(Purchase purchase, IReadOnlyList<StockMovement> stockMovements,
        IReadOnlyList<CashMovement> cashMovements, DateTime now, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO purchases (id, number, date, supplier_id, currency, rate_cup_per_usd_cents, location_id,
                                   cash_box_id, goods_total_cents, notes, status, void_reason, voided_at,
                                   created_at, updated_at)
            VALUES (@Id, @Number, @Date, @SupplierId, @Currency, @RateCupPerUsdCents, @LocationId,
                    @CashBoxId, @GoodsTotalCents, @Notes, @Status, NULL, NULL, @CreatedAt, @UpdatedAt)
            """, new
        {
            purchase.Id,
            purchase.Number,
            purchase.Date,
            purchase.SupplierId,
            Currency = purchase.Currency.ToString(),
            purchase.RateCupPerUsdCents,
            purchase.LocationId,
            purchase.CashBoxId,
            purchase.GoodsTotalCents,
            purchase.Notes,
            Status = purchase.Status.ToString(),
            purchase.CreatedAt,
            purchase.UpdatedAt
        }, transaction, cancellationToken: cancellationToken));

        foreach (var line in purchase.Lines)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO purchase_lines (id, purchase_id, line_no, product_id, purchase_unit, stock_unit,
                                            quantity_milli, conversion_factor_milli, stock_quantity_milli,
                                            unit_cost_cents, line_total_cents, allocated_charges_usd_micros,
                                            landed_unit_cost_usd_micros, landed_unit_cost_cup_micros)
                VALUES (@Id, @PurchaseId, @LineNo, @ProductId, @PurchaseUnit, @StockUnit,
                        @QuantityMilli, @ConversionFactorMilli, @StockQuantityMilli,
                        @UnitCostCents, @LineTotalCents, @AllocatedChargesUsdMicros,
                        @LandedUnitCostUsdMicros, @LandedUnitCostCupMicros)
                """, new
            {
                line.Id,
                line.PurchaseId,
                line.LineNo,
                line.ProductId,
                PurchaseUnit = line.PurchaseUnit.ToString(),
                StockUnit = line.StockUnit.ToString(),
                line.QuantityMilli,
                line.ConversionFactorMilli,
                line.StockQuantityMilli,
                line.UnitCostCents,
                line.LineTotalCents,
                line.AllocatedChargesUsdMicros,
                line.LandedUnitCostUsdMicros,
                line.LandedUnitCostCupMicros
            }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var charge in purchase.Charges)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO purchase_charges (id, purchase_id, description, currency, amount_cents, cash_box_id)
                VALUES (@Id, @PurchaseId, @Description, @Currency, @AmountCents, @CashBoxId)
                """, new
            {
                charge.Id,
                charge.PurchaseId,
                charge.Description,
                Currency = charge.Currency.ToString(),
                charge.AmountCents,
                charge.CashBoxId
            }, transaction, cancellationToken: cancellationToken));
        }

        await InsertMovementsAsync(connection, transaction, stockMovements, cashMovements, cancellationToken);
        await RecalculateCostsAsync(connection, transaction, purchase, now, cancellationToken);

        transaction.Commit();
    }

    public async Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var purchase = await connection.QuerySingleOrDefaultAsync<Purchase>(new CommandDefinition("""
            SELECT p.id, p.number, p.date, p.supplier_id, s.name AS supplier_name, p.currency,
                   p.rate_cup_per_usd_cents, p.location_id, p.cash_box_id, p.goods_total_cents, p.notes,
                   p.status, p.void_reason, p.voided_at, p.created_at, p.updated_at
            FROM purchases p
                JOIN suppliers s ON s.id = p.supplier_id
            WHERE p.id = @id
            """, new { id }, cancellationToken: cancellationToken));

        if (purchase is null)
        {
            return null;
        }

        var lines = await connection.QueryAsync<PurchaseLine>(new CommandDefinition("""
            SELECT l.id, l.purchase_id, l.line_no, l.product_id, pr.name AS product_name, pr.code AS product_code,
                   l.purchase_unit, l.stock_unit, l.quantity_milli, l.conversion_factor_milli,
                   l.stock_quantity_milli, l.unit_cost_cents, l.line_total_cents, l.allocated_charges_usd_micros,
                   l.landed_unit_cost_usd_micros, l.landed_unit_cost_cup_micros
            FROM purchase_lines l
                JOIN products pr ON pr.id = l.product_id
            WHERE l.purchase_id = @id
            ORDER BY l.line_no
            """, new { id }, cancellationToken: cancellationToken));

        var charges = await connection.QueryAsync<PurchaseCharge>(new CommandDefinition("""
            SELECT id, purchase_id, description, currency, amount_cents, cash_box_id
            FROM purchase_charges
            WHERE purchase_id = @id
            ORDER BY rowid
            """, new { id }, cancellationToken: cancellationToken));

        purchase.Lines.AddRange(lines);
        purchase.Charges.AddRange(charges);
        return purchase;
    }

    public async Task<IEnumerable<PurchaseSummary>> GetAllAsync(GetAllPurchasesOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<PurchaseSummary>(new CommandDefinition($"""
            SELECT p.id, p.number, p.date, p.supplier_id, s.name AS supplier_name, p.currency,
                   p.goods_total_cents, p.status,
                   (SELECT COUNT(*) FROM purchase_lines l WHERE l.purchase_id = p.id) AS line_count,
                   (SELECT group_concat(pr.name, ', ') FROM purchase_lines l JOIN products pr ON pr.id = l.product_id
                    WHERE l.purchase_id = p.id) AS product_names
            FROM purchases p
                JOIN suppliers s ON s.id = p.supplier_id
            WHERE {ListFilter}
            ORDER BY p.date DESC, p.number DESC
            LIMIT @pageSize OFFSET @offset
            """, ToFilterParameters(options, paged: true), cancellationToken: cancellationToken));
    }

    public async Task<int> GetCountAsync(GetAllPurchasesOptions options, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM purchases p WHERE {ListFilter}",
            ToFilterParameters(options, paged: false), cancellationToken: cancellationToken));
    }

    public async Task VoidAsync(Purchase purchase, IReadOnlyList<StockMovement> stockMovements,
        IReadOnlyList<CashMovement> cashMovements, DateTime now, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // The status flips FIRST: the cost replay below already leaves this purchase out.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE purchases SET
                status = 'Voided',
                void_reason = @VoidReason,
                voided_at = @VoidedAt,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new { purchase.Id, purchase.VoidReason, purchase.VoidedAt, purchase.UpdatedAt },
            transaction, cancellationToken: cancellationToken));

        await InsertMovementsAsync(connection, transaction, stockMovements, cashMovements, cancellationToken);
        await RecalculateCostsAsync(connection, transaction, purchase, now, cancellationToken);

        transaction.Commit();
    }

    public async Task<IEnumerable<ProductPurchase>> GetProductPurchasesAsync(Guid productId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<ProductPurchase>(new CommandDefinition("""
            SELECT p.id AS purchase_id, p.number AS purchase_number, p.date, s.name AS supplier_name, p.currency,
                   p.rate_cup_per_usd_cents, l.quantity_milli, l.purchase_unit, l.stock_quantity_milli, l.stock_unit,
                   l.unit_cost_cents, l.landed_unit_cost_usd_micros, l.landed_unit_cost_cup_micros, p.status
            FROM purchase_lines l
                JOIN purchases p ON p.id = l.purchase_id
                JOIN suppliers s ON s.id = p.supplier_id
            WHERE l.product_id = @productId
            ORDER BY p.date DESC, p.number DESC
            """, new { productId }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<SupplierProduct>> GetSupplierProductsAsync(Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        // The "last" line per product: the one of the most recent purchase (date, then number).
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<SupplierProduct>(new CommandDefinition("""
            WITH ranked AS (
                SELECT l.product_id, p.date, l.landed_unit_cost_usd_micros,
                       ROW_NUMBER() OVER (PARTITION BY l.product_id ORDER BY p.date DESC, p.number DESC) AS rn,
                       COUNT(*) OVER (PARTITION BY l.product_id) AS purchase_count
                FROM purchase_lines l
                    JOIN purchases p ON p.id = l.purchase_id
                WHERE p.supplier_id = @supplierId AND p.status = 'Confirmed'
            )
            SELECT r.product_id, pr.name AS product_name, pr.code AS product_code, pr.stock_unit,
                   r.date AS last_purchase_date, r.landed_unit_cost_usd_micros AS last_landed_unit_cost_usd_micros,
                   r.purchase_count
            FROM ranked r
                JOIN products pr ON pr.id = r.product_id
            WHERE r.rn = 1
            ORDER BY pr.name COLLATE NOCASE
            """, new { supplierId }, cancellationToken: cancellationToken));
    }

    private static async Task InsertMovementsAsync(IDbConnection connection, IDbTransaction transaction,
        IReadOnlyList<StockMovement> stockMovements, IReadOnlyList<CashMovement> cashMovements,
        CancellationToken cancellationToken)
    {
        await StockLedger.AppendAsync(connection, transaction, stockMovements, cancellationToken);
        await CashLedger.AppendAsync(connection, transaction, cashMovements, cancellationToken);
    }

    private static Task RecalculateCostsAsync(IDbConnection connection, IDbTransaction transaction,
        Purchase purchase, DateTime now, CancellationToken cancellationToken) =>
        StockLedger.RecalculateCostsAsync(connection, transaction, purchase.Lines.Select(line => line.ProductId), now,
            cancellationToken);

    private static object ToFilterParameters(GetAllPurchasesOptions options, bool paged) => new
    {
        from = options.From,
        to = options.To,
        supplierId = options.SupplierId,
        productId = options.ProductId,
        categoryId = options.CategoryId,
        includeVoided = options.IncludeVoided,
        pageSize = paged ? options.PageSize : 0,
        offset = paged ? (options.Page - 1) * options.PageSize : 0
    };
}
