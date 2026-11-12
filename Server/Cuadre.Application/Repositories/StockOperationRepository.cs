using System.Data;
using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IStockOperationRepository
{
    /// <summary>MAX + 1, one sequence for every kind. number is UNIQUE: a clash fails instead of duplicating.</summary>
    Task<int> GetNextNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>Header, lines, movements and the replayed costs of its products: ONE transaction.</summary>
    Task CreateAsync(StockOperation operation, IReadOnlyList<StockMovement> movements, DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>With location names and lines (product name, code and units).</summary>
    Task<StockOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<StockOperationSummary>> GetAllAsync(GetAllStockOperationsOptions options,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllStockOperationsOptions options, CancellationToken cancellationToken = default);

    /// <summary>Marks it voided, writes the reversal movements and replays the costs. One transaction.</summary>
    Task VoidAsync(StockOperation operation, IReadOnlyList<StockMovement> movements, DateTime now,
        CancellationToken cancellationToken = default);
}

public class StockOperationRepository : IStockOperationRepository
{
    // Shared by GetAll and GetCount so the total always matches the page.
    private const string ListFilter = """
        (@kind IS NULL OR o.kind = @kind)
        AND (@from IS NULL OR o.date >= @from)
        AND (@to IS NULL OR o.date <= @to)
        AND (@locationId IS NULL OR o.location_id = @locationId OR o.to_location_id = @locationId)
        AND (@includeVoided = 1 OR o.status = 'Confirmed')
        AND (@productId IS NULL OR EXISTS (
                SELECT 1 FROM stock_operation_lines fl WHERE fl.operation_id = o.id AND fl.product_id = @productId))
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public StockOperationRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<int> GetNextNumberAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COALESCE(MAX(number), 0) + 1 FROM stock_operations", cancellationToken: cancellationToken));
    }

    public async Task CreateAsync(StockOperation operation, IReadOnlyList<StockMovement> movements, DateTime now,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO stock_operations (id, number, kind, date, location_id, to_location_id, exit_reason, notes,
                                          status, void_reason, voided_at, created_at, updated_at)
            VALUES (@Id, @Number, @Kind, @Date, @LocationId, @ToLocationId, @ExitReason, @Notes,
                    @Status, NULL, NULL, @CreatedAt, @UpdatedAt)
            """, new
        {
            operation.Id,
            operation.Number,
            Kind = operation.Kind.ToString(),
            operation.Date,
            operation.LocationId,
            operation.ToLocationId,
            ExitReason = operation.ExitReason?.ToString(),
            operation.Notes,
            Status = operation.Status.ToString(),
            operation.CreatedAt,
            operation.UpdatedAt
        }, transaction, cancellationToken: cancellationToken));

        foreach (var line in operation.Lines)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO stock_operation_lines (id, operation_id, line_no, product_id, entered_quantity_milli,
                                                   entered_in_purchase_units, conversion_factor_milli, quantity_milli,
                                                   expected_milli, counted_milli, unit_cost_usd_micros,
                                                   unit_cost_cup_micros)
                VALUES (@Id, @OperationId, @LineNo, @ProductId, @EnteredQuantityMilli,
                        @EnteredInPurchaseUnits, @ConversionFactorMilli, @QuantityMilli,
                        @ExpectedMilli, @CountedMilli, @UnitCostUsdMicros, @UnitCostCupMicros)
                """, new
            {
                line.Id,
                line.OperationId,
                line.LineNo,
                line.ProductId,
                line.EnteredQuantityMilli,
                line.EnteredInPurchaseUnits,
                line.ConversionFactorMilli,
                line.QuantityMilli,
                line.ExpectedMilli,
                line.CountedMilli,
                line.UnitCostUsdMicros,
                line.UnitCostCupMicros
            }, transaction, cancellationToken: cancellationToken));
        }

        await StockLedger.AppendAsync(connection, transaction, movements, cancellationToken);
        await StockLedger.RecalculateCostsAsync(connection, transaction,
            operation.Lines.Select(line => line.ProductId), now, cancellationToken);

        transaction.Commit();
    }

    public async Task<StockOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var operation = await connection.QuerySingleOrDefaultAsync<StockOperation>(new CommandDefinition("""
            SELECT o.id, o.number, o.kind, o.date, o.location_id, l.name AS location_name, o.to_location_id,
                   t.name AS to_location_name, o.exit_reason, o.notes, o.status, o.void_reason, o.voided_at,
                   o.created_at, o.updated_at
            FROM stock_operations o
                JOIN locations l ON l.id = o.location_id
                LEFT JOIN locations t ON t.id = o.to_location_id
            WHERE o.id = @id
            """, new { id }, cancellationToken: cancellationToken));

        if (operation is null)
        {
            return null;
        }

        var lines = await connection.QueryAsync<StockOperationLine>(new CommandDefinition("""
            SELECT ol.id, ol.operation_id, ol.line_no, ol.product_id, pr.name AS product_name, pr.code AS product_code,
                   pr.purchase_unit, pr.stock_unit, ol.entered_quantity_milli, ol.entered_in_purchase_units,
                   ol.conversion_factor_milli, ol.quantity_milli, ol.expected_milli, ol.counted_milli,
                   ol.unit_cost_usd_micros, ol.unit_cost_cup_micros
            FROM stock_operation_lines ol
                JOIN products pr ON pr.id = ol.product_id
            WHERE ol.operation_id = @id
            ORDER BY ol.line_no
            """, new { id }, cancellationToken: cancellationToken));

        operation.Lines.AddRange(lines);
        return operation;
    }

    public async Task<IEnumerable<StockOperationSummary>> GetAllAsync(GetAllStockOperationsOptions options,
        CancellationToken cancellationToken = default)
    {
        // Value in whole CUP cents: Σ quantity × cost. Exits are positive (what left); counts are negated so that a
        // shortage (negative difference) reads as a positive loss.
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<StockOperationSummary>(new CommandDefinition($"""
            SELECT o.id, o.number, o.kind, o.date, o.location_id, l.name AS location_name, o.to_location_id,
                   t.name AS to_location_name, o.exit_reason, o.status,
                   (SELECT COUNT(*) FROM stock_operation_lines ol WHERE ol.operation_id = o.id) AS line_count,
                   (SELECT group_concat(pr.name, ', ') FROM stock_operation_lines ol
                        JOIN products pr ON pr.id = ol.product_id
                    WHERE ol.operation_id = o.id) AS product_names,
                   CASE o.kind
                       WHEN 'Transfer' THEN NULL
                       ELSE (SELECT CAST(ROUND(SUM(ol.quantity_milli * ol.unit_cost_cup_micros / 10000000.0)
                                               * (CASE o.kind WHEN 'Count' THEN -1 ELSE 1 END)) AS INTEGER)
                             FROM stock_operation_lines ol
                             WHERE ol.operation_id = o.id AND ol.unit_cost_cup_micros IS NOT NULL)
                   END AS value_cup_cents
            FROM stock_operations o
                JOIN locations l ON l.id = o.location_id
                LEFT JOIN locations t ON t.id = o.to_location_id
            WHERE {ListFilter}
            ORDER BY o.date DESC, o.number DESC
            LIMIT @pageSize OFFSET @offset
            """, ToFilterParameters(options, paged: true), cancellationToken: cancellationToken));
    }

    public async Task<int> GetCountAsync(GetAllStockOperationsOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM stock_operations o WHERE {ListFilter}",
            ToFilterParameters(options, paged: false), cancellationToken: cancellationToken));
    }

    public async Task VoidAsync(StockOperation operation, IReadOnlyList<StockMovement> movements, DateTime now,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // The status flips FIRST: the cost replay below already leaves this operation out.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE stock_operations SET
                status = 'Voided',
                void_reason = @VoidReason,
                voided_at = @VoidedAt,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new { operation.Id, operation.VoidReason, operation.VoidedAt, operation.UpdatedAt },
            transaction, cancellationToken: cancellationToken));

        await StockLedger.AppendAsync(connection, transaction, movements, cancellationToken);
        await StockLedger.RecalculateCostsAsync(connection, transaction,
            operation.Lines.Select(line => line.ProductId), now, cancellationToken);

        transaction.Commit();
    }

    private static object ToFilterParameters(GetAllStockOperationsOptions options, bool paged) => new
    {
        kind = options.Kind?.ToString(),
        from = options.From,
        to = options.To,
        locationId = options.LocationId,
        productId = options.ProductId,
        includeVoided = options.IncludeVoided,
        pageSize = paged ? options.PageSize : 0,
        offset = paged ? (options.Page - 1) * options.PageSize : 0
    };
}
