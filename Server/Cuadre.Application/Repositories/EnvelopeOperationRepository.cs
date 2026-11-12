using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IEnvelopeOperationRepository
{
    /// <summary>MAX + 1, one sequence for every kind. number is UNIQUE: a clash fails instead of duplicating.</summary>
    Task<int> GetNextNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>Header, lines and cash movements: ONE transaction.</summary>
    Task CreateAsync(EnvelopeOperation operation, IReadOnlyList<CashMovement> movements,
        CancellationToken cancellationToken = default);

    /// <summary>With box and envelope names, and the lines.</summary>
    Task<EnvelopeOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<EnvelopeOperationSummary>> GetAllAsync(GetAllEnvelopeOperationsOptions options,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(GetAllEnvelopeOperationsOptions options, CancellationToken cancellationToken = default);

    /// <summary>Marks it voided and writes the reversal movements. One transaction.</summary>
    Task VoidAsync(EnvelopeOperation operation, IReadOnlyList<CashMovement> movements,
        CancellationToken cancellationToken = default);
}

public class EnvelopeOperationRepository : IEnvelopeOperationRepository
{
    // Shared by GetAll and GetCount so the total always matches the page.
    private const string ListFilter = """
        (@kind IS NULL OR o.kind = @kind)
        AND (@from IS NULL OR o.date >= @from)
        AND (@to IS NULL OR o.date <= @to)
        AND (@includeVoided = 1 OR o.status = 'Confirmed')
        AND (@envelopeId IS NULL OR EXISTS (
                SELECT 1 FROM envelope_operation_lines fl WHERE fl.operation_id = o.id AND fl.envelope_id = @envelopeId))
        """;

    private const string HeaderColumns = """
        o.id, o.number, o.kind, o.date, o.cash_box_id, b.name AS cash_box_name, b.currency,
        o.envelope_id, e.name AS envelope_name, o.to_envelope_id, t.name AS to_envelope_name, o.amount_cents,
        o.concept, o.status
        """;

    private const string HeaderJoins = """
        LEFT JOIN cash_boxes b ON b.id = o.cash_box_id
        LEFT JOIN envelopes e ON e.id = o.envelope_id
        LEFT JOIN envelopes t ON t.id = o.to_envelope_id
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public EnvelopeOperationRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<int> GetNextNumberAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COALESCE(MAX(number), 0) + 1 FROM envelope_operations", cancellationToken: cancellationToken));
    }

    public async Task CreateAsync(EnvelopeOperation operation, IReadOnlyList<CashMovement> movements,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO envelope_operations (id, number, kind, date, cash_box_id, envelope_id, to_envelope_id,
                                             amount_cents, concept, receipt, notes, status, void_reason, voided_at,
                                             created_at, updated_at)
            VALUES (@Id, @Number, @Kind, @Date, @CashBoxId, @EnvelopeId, @ToEnvelopeId,
                    @AmountCents, @Concept, @Receipt, @Notes, @Status, NULL, NULL,
                    @CreatedAt, @UpdatedAt)
            """, new
        {
            operation.Id,
            operation.Number,
            Kind = operation.Kind.ToString(),
            operation.Date,
            operation.CashBoxId,
            operation.EnvelopeId,
            operation.ToEnvelopeId,
            operation.AmountCents,
            operation.Concept,
            operation.Receipt,
            operation.Notes,
            Status = operation.Status.ToString(),
            operation.CreatedAt,
            operation.UpdatedAt
        }, transaction, cancellationToken: cancellationToken));

        foreach (var line in operation.Lines)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO envelope_operation_lines (id, operation_id, line_no, cash_box_id, envelope_id,
                                                      amount_cents, target_cents)
                VALUES (@Id, @OperationId, @LineNo, @CashBoxId, @EnvelopeId, @AmountCents, @TargetCents)
                """, new
            {
                line.Id,
                line.OperationId,
                line.LineNo,
                line.CashBoxId,
                line.EnvelopeId,
                line.AmountCents,
                line.TargetCents
            }, transaction, cancellationToken: cancellationToken));
        }

        await CashLedger.AppendAsync(connection, transaction, movements, cancellationToken);
        transaction.Commit();
    }

    public async Task<EnvelopeOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var operation = await connection.QuerySingleOrDefaultAsync<EnvelopeOperation>(new CommandDefinition($"""
            SELECT {HeaderColumns}, o.receipt, o.notes, o.void_reason, o.voided_at, o.created_at, o.updated_at
            FROM envelope_operations o
                {HeaderJoins}
            WHERE o.id = @id
            """, new { id }, cancellationToken: cancellationToken));

        if (operation is null)
        {
            return null;
        }

        var lines = await connection.QueryAsync<EnvelopeOperationLine>(new CommandDefinition("""
            SELECT l.id, l.operation_id, l.line_no, l.cash_box_id, b.name AS cash_box_name, b.currency,
                   l.envelope_id, e.name AS envelope_name, l.amount_cents, l.target_cents
            FROM envelope_operation_lines l
                JOIN cash_boxes b ON b.id = l.cash_box_id
                JOIN envelopes e ON e.id = l.envelope_id
            WHERE l.operation_id = @id
            ORDER BY l.line_no
            """, new { id }, cancellationToken: cancellationToken));

        operation.Lines.AddRange(lines);
        return operation;
    }

    public async Task<IEnumerable<EnvelopeOperationSummary>> GetAllAsync(GetAllEnvelopeOperationsOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<EnvelopeOperationSummary>(new CommandDefinition($"""
            SELECT {HeaderColumns},
                   (SELECT COUNT(*) FROM envelope_operation_lines l WHERE l.operation_id = o.id) AS line_count
            FROM envelope_operations o
                {HeaderJoins}
            WHERE {ListFilter}
            ORDER BY o.date DESC, o.number DESC
            LIMIT @pageSize OFFSET @offset
            """, ToFilterParameters(options, paged: true), cancellationToken: cancellationToken));
    }

    public async Task<int> GetCountAsync(GetAllEnvelopeOperationsOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM envelope_operations o WHERE {ListFilter}",
            ToFilterParameters(options, paged: false), cancellationToken: cancellationToken));
    }

    public async Task VoidAsync(EnvelopeOperation operation, IReadOnlyList<CashMovement> movements,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE envelope_operations SET
                status = 'Voided',
                void_reason = @VoidReason,
                voided_at = @VoidedAt,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new { operation.Id, operation.VoidReason, operation.VoidedAt, operation.UpdatedAt },
            transaction, cancellationToken: cancellationToken));

        await CashLedger.AppendAsync(connection, transaction, movements, cancellationToken);
        transaction.Commit();
    }

    private static object ToFilterParameters(GetAllEnvelopeOperationsOptions options, bool paged) => new
    {
        kind = options.Kind?.ToString(),
        from = options.From,
        to = options.To,
        envelopeId = options.EnvelopeId,
        includeVoided = options.IncludeVoided,
        pageSize = paged ? options.PageSize : 0,
        offset = paged ? (options.Page - 1) * options.PageSize : 0
    };
}
