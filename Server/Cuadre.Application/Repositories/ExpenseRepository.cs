using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface IExpenseRepository
{
    /// <summary>MAX + 1. number is UNIQUE: a clash fails instead of duplicating.</summary>
    Task<int> GetNextNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>The expense and its cash movement: ONE transaction.</summary>
    Task CreateAsync(Expense expense, IReadOnlyList<CashMovement> movements,
        CancellationToken cancellationToken = default);

    /// <summary>With category, box, envelope and recurring names.</summary>
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<Expense>> GetAllAsync(GetAllExpensesOptions options, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(GetAllExpensesOptions options, CancellationToken cancellationToken = default);

    /// <summary>The confirmed expenses of the filter (paging ignored), per currency.</summary>
    Task<IReadOnlyList<ExpenseTotal>> GetTotalsAsync(GetAllExpensesOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>Marks it voided and writes the reversal. One transaction.</summary>
    Task VoidAsync(Expense expense, IReadOnlyList<CashMovement> movements,
        CancellationToken cancellationToken = default);
}

public class ExpenseRepository : IExpenseRepository
{
    // Shared by GetAll, GetCount and GetTotals so the totals always match the list.
    private const string ListFilter = """
        (@from IS NULL OR x.date >= @from)
        AND (@to IS NULL OR x.date <= @to)
        AND (@categoryId IS NULL OR x.category_id = @categoryId)
        AND (@cashBoxId IS NULL OR x.cash_box_id = @cashBoxId)
        AND (@envelopeId IS NULL OR x.envelope_id = @envelopeId)
        AND (@recurringExpenseId IS NULL OR x.recurring_expense_id = @recurringExpenseId)
        """;

    private const string SelectColumns = """
        x.id, x.number, x.date, x.category_id, c.name AS category_name, x.description, x.amount_cents,
        x.cash_box_id, b.name AS cash_box_name, b.currency, x.envelope_id, e.name AS envelope_name, x.notes,
        x.recurring_expense_id, r.name AS recurring_expense_name, x.period_start,
        x.status, x.void_reason, x.voided_at, x.created_at, x.updated_at
        """;

    private const string Joins = """
        JOIN expense_categories c ON c.id = x.category_id
        JOIN cash_boxes b ON b.id = x.cash_box_id
        JOIN envelopes e ON e.id = x.envelope_id
        LEFT JOIN recurring_expenses r ON r.id = x.recurring_expense_id
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public ExpenseRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<int> GetNextNumberAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COALESCE(MAX(number), 0) + 1 FROM expenses", cancellationToken: cancellationToken));
    }

    public async Task CreateAsync(Expense expense, IReadOnlyList<CashMovement> movements,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO expenses (id, number, date, category_id, description, amount_cents, cash_box_id, envelope_id,
                                  notes, recurring_expense_id, period_start, status, void_reason, voided_at,
                                  created_at, updated_at)
            VALUES (@Id, @Number, @Date, @CategoryId, @Description, @AmountCents, @CashBoxId, @EnvelopeId,
                    @Notes, @RecurringExpenseId, @PeriodStart, @Status, NULL, NULL,
                    @CreatedAt, @UpdatedAt)
            """, new
        {
            expense.Id,
            expense.Number,
            expense.Date,
            expense.CategoryId,
            expense.Description,
            expense.AmountCents,
            expense.CashBoxId,
            expense.EnvelopeId,
            expense.Notes,
            expense.RecurringExpenseId,
            expense.PeriodStart,
            Status = expense.Status.ToString(),
            expense.CreatedAt,
            expense.UpdatedAt
        }, transaction, cancellationToken: cancellationToken));

        await CashLedger.AppendAsync(connection, transaction, movements, cancellationToken);
        transaction.Commit();
    }

    public async Task<Expense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Expense>(new CommandDefinition($"""
            SELECT {SelectColumns}
            FROM expenses x
                {Joins}
            WHERE x.id = @id
            """, new { id }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Expense>> GetAllAsync(GetAllExpensesOptions options,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Expense>(new CommandDefinition($"""
            SELECT {SelectColumns}
            FROM expenses x
                {Joins}
            WHERE {ListFilter}
              AND (@includeVoided = 1 OR x.status = 'Confirmed')
            ORDER BY x.date DESC, x.number DESC
            LIMIT @pageSize OFFSET @offset
            """, ToFilterParameters(options, paged: true), cancellationToken: cancellationToken));
    }

    public async Task<int> GetCountAsync(GetAllExpensesOptions options, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<int>(new CommandDefinition($"""
            SELECT COUNT(*) FROM expenses x
            WHERE {ListFilter}
              AND (@includeVoided = 1 OR x.status = 'Confirmed')
            """, ToFilterParameters(options, paged: false), cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ExpenseTotal>> GetTotalsAsync(GetAllExpensesOptions options,
        CancellationToken cancellationToken = default)
    {
        // Voided expenses never count, even when the list shows them.
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<TotalRow>(new CommandDefinition($"""
            SELECT b.currency, SUM(x.amount_cents) AS amount_cents
            FROM expenses x
                JOIN cash_boxes b ON b.id = x.cash_box_id
            WHERE {ListFilter}
              AND x.status = 'Confirmed'
            GROUP BY b.currency
            ORDER BY b.currency
            """, ToFilterParameters(options, paged: false), cancellationToken: cancellationToken));

        return rows.Select(row => new ExpenseTotal(row.Currency, row.AmountCents)).ToList();
    }

    public async Task VoidAsync(Expense expense, IReadOnlyList<CashMovement> movements,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE expenses SET
                status = 'Voided',
                void_reason = @VoidReason,
                voided_at = @VoidedAt,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new { expense.Id, expense.VoidReason, expense.VoidedAt, expense.UpdatedAt },
            transaction, cancellationToken: cancellationToken));

        await CashLedger.AppendAsync(connection, transaction, movements, cancellationToken);
        transaction.Commit();
    }

    private sealed class TotalRow
    {
        public Currency Currency { get; init; }
        public long AmountCents { get; init; }
    }

    private static object ToFilterParameters(GetAllExpensesOptions options, bool paged) => new
    {
        from = options.From,
        to = options.To,
        categoryId = options.CategoryId,
        cashBoxId = options.CashBoxId,
        envelopeId = options.EnvelopeId,
        recurringExpenseId = options.RecurringExpenseId,
        includeVoided = options.IncludeVoided,
        pageSize = paged ? options.PageSize : 0,
        offset = paged ? (options.Page - 1) * options.PageSize : 0
    };
}
