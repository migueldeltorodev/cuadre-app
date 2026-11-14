using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>
/// Slice 8: read-only queries for the dashboard and the reports. Only CONFIRMED documents count: voided closings,
/// purchases, operations and expenses never show up, and voided discrepancies neither.
/// </summary>
public interface IReportRepository
{
    Task<IReadOnlyList<DailyFigures>> GetDaysAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    Task<DailyFigures?> GetLastClosingAsync(CancellationToken cancellationToken = default);

    /// <summary>Only the months with closings or expenses; the service fills the gaps.</summary>
    Task<IReadOnlyList<MonthTotals>> GetMonthTotalsAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpenseCategoryTotal>> GetExpensesByCategoryAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    Task<DiscrepancySummary> GetDiscrepancySummaryAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LostShortage>> GetLostShortagesAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Exit lines (any reason) and count lines with a difference; the model decides what is a loss.</summary>
    Task<IReadOnlyList<StockLossRow>> GetStockLossRowsAsync(DateOnly from, DateOnly to, Guid? categoryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductSalesRow>> GetProductSalesAsync(DateOnly from, DateOnly to, Guid? categoryId,
        CancellationToken cancellationToken = default);

    /// <summary>One product, oldest first. Null dates = no limit.</summary>
    Task<IReadOnlyList<PurchasePoint>> GetPurchasePointsAsync(Guid productId, DateOnly? from, DateOnly? to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPriceChange>> GetPriceChangesAsync(Guid productId, DateOnly? from, DateOnly? to,
        CancellationToken cancellationToken = default);

    /// <summary>Many products, with their code, name, unit and current price.</summary>
    Task<IReadOnlyList<PurchasePoint>> GetPurchasePointsAsync(GetCostChangesOptions options,
        CancellationToken cancellationToken = default);

    // ---------- Accountant summary (D-63) ----------

    /// <summary>What the closings collected with each payment method, the biggest first.</summary>
    Task<IReadOnlyList<IncomeByMethod>> GetIncomeByMethodAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>A2 of the closings and the colchón inside it.</summary>
    Task<CostOfSalesSplit> GetCostOfSalesSplitAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Every confirmed expense, with the rate of its day (or the last one before) for the USD ones.</summary>
    Task<IReadOnlyList<ExpenseLine>> GetExpenseLinesAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>The goods and the charges of every confirmed purchase, with the purchase's rate.</summary>
    Task<IReadOnlyList<PurchaseAmountRow>> GetPurchaseAmountRowsAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>The goods of each location at the end of <paramref name="date"/>, at FIFO cost (D-60).</summary>
    Task<IReadOnlyList<LocationValue>> GetInventoryValueAsync(DateOnly date,
        CancellationToken cancellationToken = default);
}

public class ReportRepository : IReportRepository
{
    // A category filter that also takes its subcategories (like the product list).
    private const string CategoryFilter =
        "(@categoryId IS NULL OR p.category_id = @categoryId OR p.subcategory_id = @categoryId)";

    private const string DayColumns = """
        c.id AS closing_id, c.date, c.cash_collected_cents, c.transfer_collected_cents, c.cost_of_sales_cents,
        c.theoretical_sales_cents, c.adjusted_difference_cents, c.staff_pay_cents
        """;

    // What came back of each shortage (confirmed recoveries only, on any day).
    private const string RecoveredJoin = """
        LEFT JOIN (SELECT discrepancy_id, SUM(amount_cents) AS recovered_cents
                   FROM cash_discrepancy_recoveries
                   WHERE status = 'Confirmed'
                   GROUP BY discrepancy_id) rec ON rec.discrepancy_id = d.id
        """;

    private const string PurchasePointColumns = """
        pu.id AS purchase_id, pu.number AS purchase_number, pu.date, pu.supplier_id, s.name AS supplier_name,
        pu.currency, pu.rate_cup_per_usd_cents, l.stock_quantity_milli,
        l.landed_unit_cost_usd_micros, l.landed_unit_cost_cup_micros
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public ReportRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<IReadOnlyList<DailyFigures>> GetDaysAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DailyFigures>(new CommandDefinition($"""
            SELECT {DayColumns}
            FROM daily_closings c
            WHERE c.status = 'Confirmed' AND c.date BETWEEN @from AND @to
            ORDER BY c.date
            """, new { from, to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<DailyFigures?> GetLastClosingAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DailyFigures>(new CommandDefinition($"""
            SELECT {DayColumns}
            FROM daily_closings c
            WHERE c.status = 'Confirmed'
            ORDER BY c.date DESC
            LIMIT 1
            """, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MonthTotals>> GetMonthTotalsAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var closings = (await connection.QueryAsync<MonthClosingRow>(new CommandDefinition("""
            SELECT substr(c.date, 1, 7) AS month,
                   COUNT(*) AS closed_days,
                   SUM(c.cash_collected_cents + c.transfer_collected_cents) AS collected_cents,
                   SUM(c.cost_of_sales_cents) AS cost_of_sales_cents,
                   SUM(c.staff_pay_cents) AS staff_pay_cents
            FROM daily_closings c
            WHERE c.status = 'Confirmed' AND c.date BETWEEN @from AND @to
            GROUP BY substr(c.date, 1, 7)
            """, new { from, to }, cancellationToken: cancellationToken)))
            .ToDictionary(row => row.Month);

        var expenses = (await connection.QueryAsync<MonthExpenseRow>(new CommandDefinition("""
            SELECT substr(x.date, 1, 7) AS month, b.currency, SUM(x.amount_cents) AS amount_cents
            FROM expenses x
                JOIN cash_boxes b ON b.id = x.cash_box_id
            WHERE x.status = 'Confirmed' AND x.date BETWEEN @from AND @to
            GROUP BY substr(x.date, 1, 7), b.currency
            """, new { from, to }, cancellationToken: cancellationToken)))
            .ToList();

        return closings.Keys
            .Union(expenses.Select(row => row.Month))
            .Order(StringComparer.Ordinal)
            .Select(month =>
            {
                var closing = closings.GetValueOrDefault(month);
                long Spent(Currency currency) => expenses
                    .Where(row => row.Month == month && row.Currency == currency)
                    .Sum(row => row.AmountCents);
                return new MonthTotals(
                    int.Parse(month[..4]),
                    int.Parse(month[5..7]),
                    (int)(closing?.ClosedDays ?? 0),
                    closing?.CollectedCents ?? 0,
                    closing?.CostOfSalesCents ?? 0,
                    closing?.StaffPayCents ?? 0,
                    Spent(Currency.CUP),
                    Spent(Currency.USD));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ExpenseCategoryTotal>> GetExpensesByCategoryAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ExpenseCategoryTotal>(new CommandDefinition("""
            SELECT x.category_id, c.name AS category_name, b.currency,
                   COUNT(*) AS count, SUM(x.amount_cents) AS amount_cents
            FROM expenses x
                JOIN expense_categories c ON c.id = x.category_id
                JOIN cash_boxes b ON b.id = x.cash_box_id
            WHERE x.status = 'Confirmed' AND x.date BETWEEN @from AND @to
            GROUP BY x.category_id, c.name, b.currency
            ORDER BY b.currency, SUM(x.amount_cents) DESC, c.name
            """, new { from, to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<DiscrepancySummary> GetDiscrepancySummaryAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<DiscrepancySummary>(new CommandDefinition($"""
            SELECT COALESCE(SUM(CASE WHEN d.kind = 'Shortage' THEN 1 ELSE 0 END), 0) AS shortage_count,
                   COALESCE(SUM(CASE WHEN d.kind = 'Shortage' THEN d.amount_cents ELSE 0 END), 0) AS shortage_cents,
                   COALESCE(SUM(CASE WHEN d.kind = 'Surplus' THEN 1 ELSE 0 END), 0) AS surplus_count,
                   COALESCE(SUM(CASE WHEN d.kind = 'Surplus' THEN d.amount_cents ELSE 0 END), 0) AS surplus_cents,
                   COALESCE(SUM(CASE WHEN d.kind = 'Shortage' THEN COALESCE(rec.recovered_cents, 0) ELSE 0 END), 0)
                       AS recovered_cents,
                   COALESCE(SUM(CASE WHEN d.kind = 'Shortage' AND d.status = 'Lost'
                                     THEN d.amount_cents - COALESCE(rec.recovered_cents, 0) ELSE 0 END), 0)
                       AS lost_cents,
                   COALESCE(SUM(CASE WHEN d.kind = 'Shortage' AND d.status = 'Pending'
                                     THEN d.amount_cents - COALESCE(rec.recovered_cents, 0) ELSE 0 END), 0)
                       AS pending_cents
            FROM cash_discrepancies d
                {RecoveredJoin}
            WHERE d.status <> 'Voided' AND d.date BETWEEN @from AND @to
            """, new { from, to }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<LostShortage>> GetLostShortagesAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LostShortage>(new CommandDefinition($"""
            SELECT d.id AS discrepancy_id, d.date, d.amount_cents, COALESCE(rec.recovered_cents, 0) AS recovered_cents,
                   e.name AS employee_name, d.resolution_note AS note
            FROM cash_discrepancies d
                {RecoveredJoin}
                LEFT JOIN employees e ON e.id = d.employee_id
            WHERE d.kind = 'Shortage' AND d.status = 'Lost' AND d.date BETWEEN @from AND @to
            ORDER BY d.date
            """, new { from, to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StockLossRow>> GetStockLossRowsAsync(DateOnly from, DateOnly to,
        Guid? categoryId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<StockLossRow>(new CommandDefinition($"""
            SELECT o.id AS operation_id, o.number AS operation_number, o.date, o.kind, o.exit_reason,
                   l.product_id, p.code AS product_code, p.name AS product_name, p.stock_unit,
                   l.quantity_milli, l.unit_cost_cup_micros
            FROM stock_operation_lines l
                JOIN stock_operations o ON o.id = l.operation_id
                JOIN products p ON p.id = l.product_id
            WHERE o.status = 'Confirmed' AND o.date BETWEEN @from AND @to
              AND (o.kind = 'Exit' OR (o.kind = 'Count' AND l.quantity_milli <> 0))
              AND {CategoryFilter}
            ORDER BY o.date, o.number, l.line_no
            """, new { from, to, categoryId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductSalesRow>> GetProductSalesAsync(DateOnly from, DateOnly to,
        Guid? categoryId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProductSalesRow>(new CommandDefinition($"""
            SELECT l.product_id, p.code, p.name, p.stock_unit,
                   SUM(l.sold_milli) AS sold_milli, SUM(l.sales_cents) AS sales_cents, SUM(l.cost_cents) AS cost_cents
            FROM daily_closing_lines l
                JOIN daily_closings c ON c.id = l.closing_id
                JOIN products p ON p.id = l.product_id
            WHERE c.status = 'Confirmed' AND c.date BETWEEN @from AND @to
              AND {CategoryFilter}
            GROUP BY l.product_id, p.code, p.name, p.stock_unit
            HAVING SUM(l.sold_milli) <> 0 OR SUM(l.sales_cents) <> 0
            """, new { from, to, categoryId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<PurchasePoint>> GetPurchasePointsAsync(Guid productId, DateOnly? from,
        DateOnly? to, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PurchasePoint>(new CommandDefinition($"""
            SELECT {PurchasePointColumns}, l.product_id
            FROM purchase_lines l
                JOIN purchases pu ON pu.id = l.purchase_id
                JOIN suppliers s ON s.id = pu.supplier_id
            WHERE pu.status = 'Confirmed' AND l.product_id = @productId
              AND (@from IS NULL OR pu.date >= @from)
              AND (@to IS NULL OR pu.date <= @to)
            ORDER BY pu.date, pu.number
            """, new { productId, from, to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductPriceChange>> GetPriceChangesAsync(Guid productId, DateOnly? from,
        DateOnly? to, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProductPriceChange>(new CommandDefinition("""
            SELECT id, product_id, price_cents, previous_price_cents, effective_date, reason, created_at
            FROM product_price_changes
            WHERE product_id = @productId
              AND (@from IS NULL OR effective_date >= @from)
              AND (@to IS NULL OR effective_date <= @to)
            ORDER BY created_at, rowid
            """, new { productId, from, to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<PurchasePoint>> GetPurchasePointsAsync(GetCostChangesOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PurchasePoint>(new CommandDefinition($"""
            SELECT {PurchasePointColumns},
                   l.product_id, p.code AS product_code, p.name AS product_name, p.stock_unit, p.sale_price_cents
            FROM purchase_lines l
                JOIN purchases pu ON pu.id = l.purchase_id
                JOIN suppliers s ON s.id = pu.supplier_id
                JOIN products p ON p.id = l.product_id
            WHERE pu.status = 'Confirmed' AND pu.date BETWEEN @from AND @to
              AND (@supplierId IS NULL OR pu.supplier_id = @supplierId)
              AND {CategoryFilter}
            ORDER BY l.product_id, pu.date, pu.number
            """, new
            {
                from = options.From,
                to = options.To,
                categoryId = options.CategoryId,
                supplierId = options.SupplierId
            }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<IncomeByMethod>> GetIncomeByMethodAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<IncomeRow>(new CommandDefinition("""
            SELECT m.id AS payment_method_id, m.name, MAX(cc.is_transfer) AS is_transfer,
                   SUM(cc.amount_cents) AS amount_cents
            FROM daily_closing_collections cc
                JOIN daily_closings c ON c.id = cc.closing_id
                JOIN payment_methods m ON m.id = cc.payment_method_id
            WHERE c.status = 'Confirmed' AND c.date BETWEEN @from AND @to
            GROUP BY m.id, m.name
            HAVING SUM(cc.amount_cents) <> 0
            ORDER BY SUM(cc.amount_cents) DESC, m.name
            """, new { from, to }, cancellationToken: cancellationToken));
        return rows
            .Select(row => new IncomeByMethod(row.PaymentMethodId, row.Name, row.IsTransfer, row.AmountCents))
            .ToList();
    }

    public async Task<CostOfSalesSplit> GetCostOfSalesSplitAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<CostOfSalesSplit>(new CommandDefinition("""
            SELECT (SELECT COALESCE(SUM(c.cost_of_sales_cents), 0)
                    FROM daily_closings c
                    WHERE c.status = 'Confirmed' AND c.date BETWEEN @from AND @to) AS cost_cents,
                   (SELECT COALESCE(SUM(l.buffer_cents), 0)
                    FROM daily_closing_lines l
                        JOIN daily_closings c ON c.id = l.closing_id
                    WHERE c.status = 'Confirmed' AND c.date BETWEEN @from AND @to) AS buffer_cents
            """, new { from, to }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ExpenseLine>> GetExpenseLinesAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ExpenseLine>(new CommandDefinition("""
            SELECT x.category_id, c.name AS category_name, x.date, b.currency, x.amount_cents,
                   CASE WHEN b.currency = 'USD'
                        THEN (SELECT r.cup_per_usd_cents FROM exchange_rates r
                              WHERE r.date <= x.date ORDER BY r.date DESC LIMIT 1)
                   END AS rate_cents
            FROM expenses x
                JOIN expense_categories c ON c.id = x.category_id
                JOIN cash_boxes b ON b.id = x.cash_box_id
            WHERE x.status = 'Confirmed' AND x.date BETWEEN @from AND @to
            ORDER BY x.date, x.number
            """, new { from, to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<PurchaseAmountRow>> GetPurchaseAmountRowsAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PurchaseAmountRow>(new CommandDefinition("""
            SELECT pu.id AS purchase_id, pu.currency, pu.goods_total_cents AS amount_cents,
                   pu.rate_cup_per_usd_cents AS rate_cents
            FROM purchases pu
            WHERE pu.status = 'Confirmed' AND pu.date BETWEEN @from AND @to
            UNION ALL
            SELECT pu.id, ch.currency, ch.amount_cents, pu.rate_cup_per_usd_cents
            FROM purchase_charges ch
                JOIN purchases pu ON pu.id = ch.purchase_id
            WHERE pu.status = 'Confirmed' AND pu.date BETWEEN @from AND @to
            """, new { from, to }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<LocationValue>> GetInventoryValueAsync(DateOnly date,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        // Same exclusions as the FIFO replay: a voided document never happened, its reversals neither.
        var rows = (await connection.QueryAsync<InventoryRow>(new CommandDefinition("""
            SELECT m.product_id, m.location_id, l.name AS location_name, SUM(m.quantity_milli) AS quantity_milli
            FROM stock_movements m
                JOIN locations l ON l.id = m.location_id
                LEFT JOIN purchases p ON p.id = m.purchase_id
                LEFT JOIN stock_operations o ON o.id = m.operation_id
                LEFT JOIN daily_closings c ON c.id = m.closing_id
            WHERE m.date <= @date
              AND (p.id IS NULL OR p.status = 'Confirmed')
              AND (o.id IS NULL OR o.status = 'Confirmed')
              AND (c.id IS NULL OR c.status = 'Confirmed')
            GROUP BY m.product_id, m.location_id, l.name
            HAVING SUM(m.quantity_milli) <> 0
            ORDER BY m.product_id, l.name
            """, new { date }, cancellationToken: cancellationToken))).ToList();

        var books = await FifoLedger.LoadAsync(connection, null, rows.Select(row => row.ProductId).ToList(), date,
            cancellationToken);
        return InventoryValuation.ByLocation(rows, books);
    }

    private class IncomeRow
    {
        public Guid PaymentMethodId { get; init; }
        public string Name { get; init; } = "";
        public bool IsTransfer { get; init; }
        public long AmountCents { get; init; }
    }

    private class MonthClosingRow
    {
        public string Month { get; init; } = "";
        public long ClosedDays { get; init; }
        public long CollectedCents { get; init; }
        public long CostOfSalesCents { get; init; }
        public long StaffPayCents { get; init; }
    }

    private class MonthExpenseRow
    {
        public string Month { get; init; } = "";
        public Currency Currency { get; init; }
        public long AmountCents { get; init; }
    }
}
