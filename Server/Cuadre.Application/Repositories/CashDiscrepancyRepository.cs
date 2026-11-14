using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>Shortages and surpluses of the closings, and the recoveries of the shortages (RF-CIE-12/13).</summary>
public interface ICashDiscrepancyRepository
{
    /// <summary>Newest first. Voided ones (their closing was reopened) are never listed.</summary>
    Task<IEnumerable<CashDiscrepancy>> GetAllAsync(GetAllCashDiscrepanciesOptions options,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllCashDiscrepanciesOptions options, CancellationToken cancellationToken = default);

    /// <summary>With its recoveries (voided ones too), oldest first.</summary>
    Task<CashDiscrepancy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ShortageRecovery?> GetRecoveryByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Shortages still to claim: how many and how much is left of them.</summary>
    Task<(int Count, long PendingCents)> GetPendingTotalsAsync(CancellationToken cancellationToken = default);

    /// <summary>A shortage of that closing already has confirmed recoveries (it cannot be reopened then).</summary>
    Task<bool> HasRecoveriesAsync(Guid closingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashMovement>> GetRecoveryMovementsAsync(Guid recoveryId,
        CancellationToken cancellationToken = default);

    /// <summary>The recovery, its money and the new status of the shortage: ONE transaction.</summary>
    Task CreateRecoveryAsync(ShortageRecovery recovery, IReadOnlyList<CashMovement> movements,
        CashDiscrepancy discrepancy, CancellationToken cancellationToken = default);

    /// <summary>Marks it voided, writes the reversals and updates the shortage: ONE transaction.</summary>
    Task VoidRecoveryAsync(ShortageRecovery recovery, IReadOnlyList<CashMovement> reversals,
        CashDiscrepancy discrepancy, CancellationToken cancellationToken = default);

    /// <summary>Status, resolution note and dates of a shortage.</summary>
    Task UpdateResolutionAsync(CashDiscrepancy discrepancy, CancellationToken cancellationToken = default);
}

public class CashDiscrepancyRepository : ICashDiscrepancyRepository
{
    private const string ListFilter = """
        d.status <> 'Voided'
        AND (@kind IS NULL OR d.kind = @kind)
        AND (@status IS NULL OR d.status = @status)
        AND (@employeeId IS NULL OR d.employee_id = @employeeId)
        AND (@from IS NULL OR d.date >= @from)
        AND (@to IS NULL OR d.date <= @to)
        """;

    private const string Columns = """
        d.id, d.closing_id, d.date, d.kind, d.amount_cents, d.employee_id, e.name AS employee_name, d.note, d.status,
        d.resolution_note, d.resolved_at, d.created_at, d.updated_at, c.date AS closing_date,
        COALESCE((SELECT SUM(r.amount_cents) FROM cash_discrepancy_recoveries r
                  WHERE r.discrepancy_id = d.id AND r.status = 'Confirmed'), 0) AS recovered_cents
        """;

    private const string Joins = """
        JOIN daily_closings c ON c.id = d.closing_id
        LEFT JOIN employees e ON e.id = d.employee_id
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public CashDiscrepancyRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<IEnumerable<CashDiscrepancy>> GetAllAsync(GetAllCashDiscrepanciesOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<CashDiscrepancy>(new CommandDefinition($"""
            SELECT {Columns}
            FROM cash_discrepancies d
                {Joins}
            WHERE {ListFilter}
            ORDER BY d.date DESC, d.created_at DESC
            LIMIT @pageSize OFFSET @offset
            """, ToFilterParameters(options, paged: true), cancellationToken: cancellationToken));
    }

    public async Task<int> GetCountAsync(GetAllCashDiscrepanciesOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM cash_discrepancies d WHERE {ListFilter}",
            ToFilterParameters(options, paged: false), cancellationToken: cancellationToken));
    }

    public async Task<CashDiscrepancy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var discrepancy = await connection.QuerySingleOrDefaultAsync<CashDiscrepancy>(new CommandDefinition($"""
            SELECT {Columns}
            FROM cash_discrepancies d
                {Joins}
            WHERE d.id = @id
            """, new { id }, cancellationToken: cancellationToken));

        if (discrepancy is null)
        {
            return null;
        }

        discrepancy.Recoveries.AddRange(await connection.QueryAsync<ShortageRecovery>(new CommandDefinition("""
            SELECT r.id, r.discrepancy_id, r.date, r.cash_box_id, b.name AS cash_box_name, r.amount_cents, r.note,
                   r.status, r.void_reason, r.voided_at, r.created_at, r.updated_at
            FROM cash_discrepancy_recoveries r
                JOIN cash_boxes b ON b.id = r.cash_box_id
            WHERE r.discrepancy_id = @id
            ORDER BY r.date, r.created_at
            """, new { id }, cancellationToken: cancellationToken)));
        return discrepancy;
    }

    public async Task<ShortageRecovery?> GetRecoveryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ShortageRecovery>(new CommandDefinition("""
            SELECT r.id, r.discrepancy_id, r.date, r.cash_box_id, b.name AS cash_box_name, r.amount_cents, r.note,
                   r.status, r.void_reason, r.voided_at, r.created_at, r.updated_at
            FROM cash_discrepancy_recoveries r
                JOIN cash_boxes b ON b.id = r.cash_box_id
            WHERE r.id = @id
            """, new { id }, cancellationToken: cancellationToken));
    }

    public async Task<(int Count, long PendingCents)> GetPendingTotalsAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync<(long Count, long PendingCents)>(new CommandDefinition("""
            SELECT COUNT(*),
                   COALESCE(SUM(d.amount_cents - COALESCE((SELECT SUM(r.amount_cents)
                                                           FROM cash_discrepancy_recoveries r
                                                           WHERE r.discrepancy_id = d.id
                                                             AND r.status = 'Confirmed'), 0)), 0)
            FROM cash_discrepancies d
            WHERE d.kind = 'Shortage' AND d.status = 'Pending'
            """, cancellationToken: cancellationToken));
        return ((int)row.Count, row.PendingCents);
    }

    public async Task<bool> HasRecoveriesAsync(Guid closingId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS (
                SELECT 1 FROM cash_discrepancy_recoveries r
                    JOIN cash_discrepancies d ON d.id = r.discrepancy_id
                WHERE d.closing_id = @closingId AND r.status = 'Confirmed')
            """, new { closingId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CashMovement>> GetRecoveryMovementsAsync(Guid recoveryId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var movements = await connection.QueryAsync<CashMovement>(new CommandDefinition("""
            SELECT id, cash_box_id, envelope_id, date, kind, amount_cents, description, purchase_id,
                   envelope_operation_id, stock_operation_id, closing_id, recovery_id, created_at
            FROM cash_movements
            WHERE recovery_id = @recoveryId
            ORDER BY rowid
            """, new { recoveryId }, cancellationToken: cancellationToken));
        return movements.ToList();
    }

    public async Task CreateRecoveryAsync(ShortageRecovery recovery, IReadOnlyList<CashMovement> movements,
        CashDiscrepancy discrepancy, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO cash_discrepancy_recoveries (id, discrepancy_id, date, cash_box_id, amount_cents, note,
                                                     status, void_reason, voided_at, created_at, updated_at)
            VALUES (@Id, @DiscrepancyId, @Date, @CashBoxId, @AmountCents, @Note, @Status, NULL, NULL,
                    @CreatedAt, @UpdatedAt)
            """, new
        {
            recovery.Id,
            recovery.DiscrepancyId,
            recovery.Date,
            recovery.CashBoxId,
            recovery.AmountCents,
            recovery.Note,
            Status = recovery.Status.ToString(),
            recovery.CreatedAt,
            recovery.UpdatedAt
        }, transaction, cancellationToken: cancellationToken));

        await CashLedger.AppendAsync(connection, transaction, movements, cancellationToken);
        await UpdateResolutionAsync(connection, transaction, discrepancy, cancellationToken);
        transaction.Commit();
    }

    public async Task VoidRecoveryAsync(ShortageRecovery recovery, IReadOnlyList<CashMovement> reversals,
        CashDiscrepancy discrepancy, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE cash_discrepancy_recoveries SET
                status = 'Voided',
                void_reason = @VoidReason,
                voided_at = @VoidedAt,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new { recovery.Id, recovery.VoidReason, recovery.VoidedAt, recovery.UpdatedAt },
            transaction, cancellationToken: cancellationToken));

        await CashLedger.AppendAsync(connection, transaction, reversals, cancellationToken);
        await UpdateResolutionAsync(connection, transaction, discrepancy, cancellationToken);
        transaction.Commit();
    }

    public async Task UpdateResolutionAsync(CashDiscrepancy discrepancy, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await UpdateResolutionAsync(connection, transaction, discrepancy, cancellationToken);
        transaction.Commit();
    }

    private static Task UpdateResolutionAsync(System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction, CashDiscrepancy discrepancy, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition("""
            UPDATE cash_discrepancies SET
                status = @Status,
                resolution_note = @ResolutionNote,
                resolved_at = @ResolvedAt,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new
        {
            discrepancy.Id,
            Status = discrepancy.Status.ToString(),
            discrepancy.ResolutionNote,
            discrepancy.ResolvedAt,
            discrepancy.UpdatedAt
        }, transaction, cancellationToken: cancellationToken));

    private static object ToFilterParameters(GetAllCashDiscrepanciesOptions options, bool paged) => new
    {
        kind = options.Kind?.ToString(),
        status = options.Status?.ToString(),
        employeeId = options.EmployeeId,
        from = options.From,
        to = options.To,
        pageSize = paged ? options.PageSize : 0,
        offset = paged ? (options.Page - 1) * options.PageSize : 0
    };
}
