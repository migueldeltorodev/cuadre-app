using System.Globalization;
using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IDailyClosingRepository
{
    /// <summary>The latest day with a confirmed closing: that day and every earlier one are locked (RF-CIE-10).</summary>
    Task<DateOnly?> GetLastClosedDateAsync(CancellationToken cancellationToken = default);

    /// <summary>The confirmed closing of that day, if any.</summary>
    Task<Guid?> GetConfirmedIdAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// RF-CIE-11: the latest confirmed closing of ANOTHER day with exactly the same cash, transfers and theoretical
    /// sales. Only asked when there were sales.
    /// </summary>
    Task<DateOnly?> FindDuplicateAsync(DateOnly date, long cashCents, long transferCents, long theoreticalSalesCents,
        CancellationToken cancellationToken = default);

    /// <summary>The first day with anything in the ledgers (stock or cash): no closing is expected before it.</summary>
    Task<DateOnly?> GetFirstActivityDateAsync(CancellationToken cancellationToken = default);

    /// <summary>What Reposición owes the other envelopes at the end of <paramref name="asOf"/> (RF-PF-09).</summary>
    Task<long> GetInternalDebtAsync(DateOnly asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every product with stock on a counter at the start of the day, or with movements there that day (RN-05).
    /// The rows of closings are left out of "in/out" (a reopened closing of the same day nets to zero anyway).
    /// </summary>
    Task<IReadOnlyList<CounterLine>> GetCounterLinesAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Active payment methods landing in an active CUP box (D-28), by name.</summary>
    Task<IReadOnlyList<ClosingPaymentMethod>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default);

    /// <summary>Header, lines, collections, split, discrepancy, both ledgers and the costs replay: ONE transaction.</summary>
    Task CreateAsync(DailyClosing closing, IReadOnlyList<StockMovement> stockMovements,
        IReadOnlyList<CashMovement> cashMovements, DateTime now, CancellationToken cancellationToken = default);

    Task<DailyClosing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<DailyClosingSummary>> GetAllAsync(GetAllDailyClosingsOptions options,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllDailyClosingsOptions options, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovement>> GetStockMovementsAsync(Guid closingId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashMovement>> GetCashMovementsAsync(Guid closingId,
        CancellationToken cancellationToken = default);

    /// <summary>Marks it voided (reopened), voids its discrepancy, writes the reversals and replays the costs.</summary>
    Task VoidAsync(DailyClosing closing, IReadOnlyList<StockMovement> stockReversals,
        IReadOnlyList<CashMovement> cashReversals, DateTime now, CancellationToken cancellationToken = default);
}

public class DailyClosingRepository : IDailyClosingRepository
{
    private const string ListFilter = """
        (@from IS NULL OR c.date >= @from)
        AND (@to IS NULL OR c.date <= @to)
        AND (@includeVoided = 1 OR c.status = 'Confirmed')
        """;

    private const string HeaderColumns = """
        c.id, c.date, c.status, c.employee_id, e.name AS employee_name, c.notes,
        c.theoretical_sales_cents, c.shrinkage_tolerance_cents, c.cash_collected_cents, c.transfer_collected_cents,
        c.cost_of_sales_cents, c.cash_difference_cents, c.adjusted_difference_cents, c.threshold_cents,
        c.staff_pay_cents, c.pay_fixed_cents, c.pay_percent_bps,
        c.debt_before_cents, c.debt_created_cents, c.debt_repaid_cents, c.debt_after_cents,
        c.void_reason, c.voided_at, c.created_at, c.updated_at
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public DailyClosingRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<DateOnly?> GetLastClosedDateAsync(CancellationToken cancellationToken = default)
    {
        // Scalars skip the type handlers: read the ISO text and parse it here.
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var text = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT MAX(date) FROM daily_closings WHERE status = 'Confirmed'", cancellationToken: cancellationToken));
        return text is null ? null : DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public async Task<Guid?> GetConfirmedIdAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var text = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT id FROM daily_closings WHERE date = @date AND status = 'Confirmed'",
            new { date }, cancellationToken: cancellationToken));
        return text is null ? null : Guid.Parse(text);
    }

    public async Task<DateOnly?> FindDuplicateAsync(DateOnly date, long cashCents, long transferCents,
        long theoreticalSalesCents, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var text = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT date FROM daily_closings
            WHERE status = 'Confirmed' AND date <> @date
              AND cash_collected_cents = @cashCents
              AND transfer_collected_cents = @transferCents
              AND theoretical_sales_cents = @theoreticalSalesCents
            ORDER BY date DESC
            LIMIT 1
            """, new { date, cashCents, transferCents, theoreticalSalesCents },
            cancellationToken: cancellationToken));
        return text is null ? null : DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public async Task<DateOnly?> GetFirstActivityDateAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var text = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT MIN(date) FROM (
                SELECT MIN(date) AS date FROM stock_movements
                UNION ALL
                SELECT MIN(date) FROM cash_movements)
            """, cancellationToken: cancellationToken));
        return text is null ? null : DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public async Task<long> GetInternalDebtAsync(DateOnly asOf, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition("""
            SELECT debt_after_cents FROM daily_closings
            WHERE status = 'Confirmed' AND date <= @asOf
            ORDER BY date DESC
            LIMIT 1
            """, new { asOf }, cancellationToken: cancellationToken)) ?? 0;
    }

    public async Task<IReadOnlyList<CounterLine>> GetCounterLinesAsync(DateOnly date,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var lines = await connection.QueryAsync<CounterLine>(new CommandDefinition("""
            WITH counters AS (
                SELECT id, name FROM locations WHERE kind = 'Counter'
            ),
            opening AS (
                SELECT m.product_id, m.location_id, SUM(m.quantity_milli) AS quantity_milli
                FROM stock_movements m
                WHERE m.date < @date AND m.location_id IN (SELECT id FROM counters)
                GROUP BY m.product_id, m.location_id
            ),
            day AS (
                SELECT m.product_id, m.location_id,
                       SUM(CASE WHEN m.quantity_milli > 0 THEN m.quantity_milli ELSE 0 END) AS in_milli,
                       SUM(CASE WHEN m.quantity_milli < 0 THEN -m.quantity_milli ELSE 0 END) AS out_milli
                FROM stock_movements m
                WHERE m.date = @date AND m.closing_id IS NULL AND m.location_id IN (SELECT id FROM counters)
                GROUP BY m.product_id, m.location_id
            ),
            keys AS (
                SELECT product_id, location_id FROM opening WHERE quantity_milli <> 0
                UNION
                SELECT product_id, location_id FROM day
            )
            SELECT k.product_id, p.code, p.name, p.stock_unit, k.location_id, l.name AS location_name,
                   COALESCE(o.quantity_milli, 0) AS opening_milli,
                   COALESCE(d.in_milli, 0) AS in_milli,
                   COALESCE(d.out_milli, 0) AS out_milli,
                   COALESCE((SELECT h.price_cents FROM product_price_changes h
                             WHERE h.product_id = p.id AND h.effective_date <= @date
                             -- rowid breaks a tie of two changes in the same millisecond: the later one wins.
                             ORDER BY h.effective_date DESC, h.created_at DESC, h.rowid DESC
                             LIMIT 1), p.sale_price_cents) AS unit_price_cents,
                   p.shrinkage_tolerance_bps AS tolerance_bps,
                   p.exchange_buffer_cents
            FROM keys k
                JOIN products p ON p.id = k.product_id
                JOIN counters l ON l.id = k.location_id
                LEFT JOIN opening o ON o.product_id = k.product_id AND o.location_id = k.location_id
                LEFT JOIN day d ON d.product_id = k.product_id AND d.location_id = k.location_id
            ORDER BY l.name, p.name
            """, new { date }, cancellationToken: cancellationToken));
        return lines.ToList();
    }

    public async Task<IReadOnlyList<ClosingPaymentMethod>> GetPaymentMethodsAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var methods = await connection.QueryAsync<ClosingPaymentMethod>(new CommandDefinition("""
            SELECT m.id, m.name, m.cash_box_id, b.name AS cash_box_name, b.kind AS cash_box_kind
            FROM payment_methods m
                JOIN cash_boxes b ON b.id = m.cash_box_id
            WHERE m.is_active = 1 AND b.is_active = 1 AND b.currency = 'CUP'
            ORDER BY b.kind, m.name
            """, cancellationToken: cancellationToken));
        return methods.ToList();
    }

    public async Task CreateAsync(DailyClosing closing, IReadOnlyList<StockMovement> stockMovements,
        IReadOnlyList<CashMovement> cashMovements, DateTime now, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO daily_closings (id, date, status, employee_id, notes,
                theoretical_sales_cents, shrinkage_tolerance_cents, cash_collected_cents, transfer_collected_cents,
                cost_of_sales_cents, cash_difference_cents, adjusted_difference_cents, threshold_cents,
                staff_pay_cents, pay_fixed_cents, pay_percent_bps,
                debt_before_cents, debt_created_cents, debt_repaid_cents, debt_after_cents,
                void_reason, voided_at, created_at, updated_at)
            VALUES (@Id, @Date, @Status, @EmployeeId, @Notes,
                @TheoreticalSalesCents, @ShrinkageToleranceCents, @CashCollectedCents, @TransferCollectedCents,
                @CostOfSalesCents, @CashDifferenceCents, @AdjustedDifferenceCents, @ThresholdCents,
                @StaffPayCents, @PayFixedCents, @PayPercentBps,
                @DebtBeforeCents, @DebtCreatedCents, @DebtRepaidCents, @DebtAfterCents,
                NULL, NULL, @CreatedAt, @UpdatedAt)
            """, new
        {
            closing.Id,
            closing.Date,
            Status = closing.Status.ToString(),
            closing.EmployeeId,
            closing.Notes,
            closing.TheoreticalSalesCents,
            closing.ShrinkageToleranceCents,
            closing.CashCollectedCents,
            closing.TransferCollectedCents,
            closing.CostOfSalesCents,
            closing.CashDifferenceCents,
            closing.AdjustedDifferenceCents,
            closing.ThresholdCents,
            closing.StaffPayCents,
            closing.PayFixedCents,
            closing.PayPercentBps,
            closing.DebtBeforeCents,
            closing.DebtCreatedCents,
            closing.DebtRepaidCents,
            closing.DebtAfterCents,
            closing.CreatedAt,
            closing.UpdatedAt
        }, transaction, cancellationToken: cancellationToken));

        foreach (var line in closing.Lines)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO daily_closing_lines (id, closing_id, line_no, product_id, location_id, stock_unit,
                    opening_milli, in_milli, out_milli, expected_milli, counted_milli, sold_milli,
                    unit_price_cents, sales_cents, tolerance_bps, tolerance_cents,
                    unit_cost_usd_micros, unit_cost_cup_micros, cost_cents, buffer_cents)
                VALUES (@Id, @ClosingId, @LineNo, @ProductId, @LocationId, @StockUnit,
                    @OpeningMilli, @InMilli, @OutMilli, @ExpectedMilli, @CountedMilli, @SoldMilli,
                    @UnitPriceCents, @SalesCents, @ToleranceBps, @ToleranceCents,
                    @UnitCostUsdMicros, @UnitCostCupMicros, @CostCents, @BufferCents)
                """, new
            {
                line.Id,
                line.ClosingId,
                line.LineNo,
                line.ProductId,
                line.LocationId,
                StockUnit = line.StockUnit.ToString(),
                line.OpeningMilli,
                line.InMilli,
                line.OutMilli,
                line.ExpectedMilli,
                line.CountedMilli,
                line.SoldMilli,
                line.UnitPriceCents,
                line.SalesCents,
                line.ToleranceBps,
                line.ToleranceCents,
                line.UnitCostUsdMicros,
                line.UnitCostCupMicros,
                line.CostCents,
                line.BufferCents
            }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var collection in closing.Collections)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO daily_closing_collections (closing_id, payment_method_id, cash_box_id, is_transfer,
                                                       amount_cents)
                VALUES (@ClosingId, @PaymentMethodId, @CashBoxId, @IsTransfer, @AmountCents)
                """, new
            {
                collection.ClosingId,
                collection.PaymentMethodId,
                collection.CashBoxId,
                collection.IsTransfer,
                collection.AmountCents
            }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var allocation in closing.Allocations)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO daily_closing_allocations (closing_id, envelope_id, percent_bps, amount_cents, cash_cents,
                                                       bank_cents)
                VALUES (@ClosingId, @EnvelopeId, @PercentBps, @AmountCents, @CashCents, @BankCents)
                """, new
            {
                allocation.ClosingId,
                allocation.EnvelopeId,
                allocation.PercentBps,
                allocation.AmountCents,
                allocation.CashCents,
                allocation.BankCents
            }, transaction, cancellationToken: cancellationToken));
        }

        if (closing.Discrepancy is { } discrepancy)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO cash_discrepancies (id, closing_id, date, kind, amount_cents, employee_id, note, status,
                                                created_at, updated_at)
                VALUES (@Id, @ClosingId, @Date, @Kind, @AmountCents, @EmployeeId, @Note, @Status,
                        @CreatedAt, @UpdatedAt)
                """, new
            {
                discrepancy.Id,
                discrepancy.ClosingId,
                discrepancy.Date,
                Kind = discrepancy.Kind.ToString(),
                discrepancy.AmountCents,
                discrepancy.EmployeeId,
                discrepancy.Note,
                Status = discrepancy.Status.ToString(),
                discrepancy.CreatedAt,
                discrepancy.UpdatedAt
            }, transaction, cancellationToken: cancellationToken));
        }

        await StockLedger.AppendAsync(connection, transaction, stockMovements, cancellationToken);
        await CashLedger.AppendAsync(connection, transaction, cashMovements, cancellationToken);
        await StockLedger.RecalculateCostsAsync(connection, transaction, stockMovements.Select(m => m.ProductId), now,
            cancellationToken);
        transaction.Commit();
    }

    public async Task<DailyClosing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var closing = await connection.QuerySingleOrDefaultAsync<DailyClosing>(new CommandDefinition($"""
            SELECT {HeaderColumns}
            FROM daily_closings c
                LEFT JOIN employees e ON e.id = c.employee_id
            WHERE c.id = @id
            """, new { id }, cancellationToken: cancellationToken));

        if (closing is null)
        {
            return null;
        }

        closing.Lines.AddRange(await connection.QueryAsync<DailyClosingLine>(new CommandDefinition("""
            SELECT l.id, l.closing_id, l.line_no, l.product_id, p.code AS product_code, p.name AS product_name,
                   l.location_id, loc.name AS location_name, l.stock_unit,
                   l.opening_milli, l.in_milli, l.out_milli, l.expected_milli, l.counted_milli, l.sold_milli,
                   l.unit_price_cents, l.sales_cents, l.tolerance_bps, l.tolerance_cents,
                   l.unit_cost_usd_micros, l.unit_cost_cup_micros, l.cost_cents, l.buffer_cents
            FROM daily_closing_lines l
                JOIN products p ON p.id = l.product_id
                JOIN locations loc ON loc.id = l.location_id
            WHERE l.closing_id = @id
            ORDER BY l.line_no
            """, new { id }, cancellationToken: cancellationToken)));

        closing.Collections.AddRange(await connection.QueryAsync<DailyClosingCollection>(new CommandDefinition("""
            SELECT k.closing_id, k.payment_method_id, m.name AS payment_method_name, k.cash_box_id,
                   b.name AS cash_box_name, k.is_transfer, k.amount_cents
            FROM daily_closing_collections k
                JOIN payment_methods m ON m.id = k.payment_method_id
                JOIN cash_boxes b ON b.id = k.cash_box_id
            WHERE k.closing_id = @id
            ORDER BY k.is_transfer, m.name
            """, new { id }, cancellationToken: cancellationToken)));

        closing.Allocations.AddRange(await connection.QueryAsync<DailyClosingAllocation>(new CommandDefinition("""
            SELECT a.closing_id, a.envelope_id, e.name AS envelope_name, e.role, e.sort_order, a.percent_bps,
                   a.amount_cents, a.cash_cents, a.bank_cents
            FROM daily_closing_allocations a
                JOIN envelopes e ON e.id = a.envelope_id
            WHERE a.closing_id = @id
            ORDER BY CASE e.role WHEN 'Replenishment' THEN 0 ELSE 1 END, e.sort_order
            """, new { id }, cancellationToken: cancellationToken)));

        closing.Discrepancy = await connection.QuerySingleOrDefaultAsync<CashDiscrepancy>(new CommandDefinition("""
            SELECT d.id, d.closing_id, d.date, d.kind, d.amount_cents, d.employee_id, d.note, d.status,
                   d.resolution_note, d.resolved_at, d.created_at, d.updated_at, d.date AS closing_date,
                   COALESCE((SELECT SUM(r.amount_cents) FROM cash_discrepancy_recoveries r
                             WHERE r.discrepancy_id = d.id AND r.status = 'Confirmed'), 0) AS recovered_cents
            FROM cash_discrepancies d
            WHERE d.closing_id = @id
            """, new { id }, cancellationToken: cancellationToken));

        return closing;
    }

    public async Task<IEnumerable<DailyClosingSummary>> GetAllAsync(GetAllDailyClosingsOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<DailyClosingSummary>(new CommandDefinition($"""
            SELECT c.id, c.date, c.status, e.name AS employee_name, c.cash_collected_cents,
                   c.transfer_collected_cents, c.cost_of_sales_cents, c.adjusted_difference_cents, c.staff_pay_cents,
                   c.debt_after_cents
            FROM daily_closings c
                LEFT JOIN employees e ON e.id = c.employee_id
            WHERE {ListFilter}
            ORDER BY c.date DESC, c.created_at DESC
            LIMIT @pageSize OFFSET @offset
            """, ToFilterParameters(options, paged: true), cancellationToken: cancellationToken));
    }

    public async Task<int> GetCountAsync(GetAllDailyClosingsOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM daily_closings c WHERE {ListFilter}",
            ToFilterParameters(options, paged: false), cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<StockMovement>> GetStockMovementsAsync(Guid closingId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var movements = await connection.QueryAsync<StockMovement>(new CommandDefinition("""
            SELECT id, product_id, location_id, date, kind, quantity_milli, unit_cost_usd_micros,
                   unit_cost_cup_micros, purchase_id, purchase_line_id, operation_id, closing_id, created_at
            FROM stock_movements
            WHERE closing_id = @closingId
            ORDER BY rowid
            """, new { closingId }, cancellationToken: cancellationToken));
        return movements.ToList();
    }

    public async Task<IReadOnlyList<CashMovement>> GetCashMovementsAsync(Guid closingId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var movements = await connection.QueryAsync<CashMovement>(new CommandDefinition("""
            SELECT id, cash_box_id, envelope_id, date, kind, amount_cents, description, purchase_id,
                   envelope_operation_id, stock_operation_id, closing_id, created_at
            FROM cash_movements
            WHERE closing_id = @closingId
            ORDER BY rowid
            """, new { closingId }, cancellationToken: cancellationToken));
        return movements.ToList();
    }

    public async Task VoidAsync(DailyClosing closing, IReadOnlyList<StockMovement> stockReversals,
        IReadOnlyList<CashMovement> cashReversals, DateTime now, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE daily_closings SET
                status = 'Voided',
                void_reason = @VoidReason,
                voided_at = @VoidedAt,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new { closing.Id, closing.VoidReason, closing.VoidedAt, closing.UpdatedAt },
            transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE cash_discrepancies SET status = 'Voided', updated_at = @now WHERE closing_id = @Id
            """, new { closing.Id, now }, transaction, cancellationToken: cancellationToken));

        await StockLedger.AppendAsync(connection, transaction, stockReversals, cancellationToken);
        await CashLedger.AppendAsync(connection, transaction, cashReversals, cancellationToken);
        await StockLedger.RecalculateCostsAsync(connection, transaction, stockReversals.Select(m => m.ProductId), now,
            cancellationToken);
        transaction.Commit();
    }

    private static object ToFilterParameters(GetAllDailyClosingsOptions options, bool paged) => new
    {
        from = options.From,
        to = options.To,
        includeVoided = options.IncludeVoided,
        pageSize = paged ? options.PageSize : 0,
        offset = paged ? (options.Page - 1) * options.PageSize : 0
    };
}
