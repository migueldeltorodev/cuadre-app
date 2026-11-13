using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>A period already dealt with: confirmed (an active expense) or skipped.</summary>
public class TakenPeriod
{
    public Guid RecurringExpenseId { get; init; }
    public DateOnly PeriodStart { get; init; }
}

/// <summary>Few rows in a lifetime: no paging. Never deleted (expenses point to them), only deactivated.</summary>
public interface IRecurringExpenseRepository
{
    Task<bool> CreateAsync(RecurringExpense recurring, CancellationToken cancellationToken = default);
    Task<RecurringExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Ignoring case.</summary>
    Task<RecurringExpense?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Active first, then by name.</summary>
    Task<IEnumerable<RecurringExpense>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(RecurringExpense recurring, CancellationToken cancellationToken = default);

    /// <summary>The periods confirmed (by an expense not voided) or skipped. All templates when the id is null.</summary>
    Task<IReadOnlyList<TakenPeriod>> GetTakenPeriodsAsync(Guid? recurringExpenseId = null,
        CancellationToken cancellationToken = default);

    Task SkipAsync(SkipRecurringExpense skip, DateTime now, CancellationToken cancellationToken = default);
}

public class RecurringExpenseRepository : IRecurringExpenseRepository
{
    private const string SelectColumns = """
        r.id, r.name, r.category_id, c.name AS category_name, r.amount_cents,
        r.cash_box_id, b.name AS cash_box_name, b.currency, r.envelope_id, e.name AS envelope_name,
        r.frequency, r.due_day, r.starts_on, r.notes, r.is_active, r.created_at, r.updated_at
        """;

    private const string Joins = """
        JOIN expense_categories c ON c.id = r.category_id
        JOIN cash_boxes b ON b.id = r.cash_box_id
        JOIN envelopes e ON e.id = r.envelope_id
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public RecurringExpenseRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(RecurringExpense recurring, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO recurring_expenses (id, name, category_id, amount_cents, cash_box_id, envelope_id, frequency,
                                            due_day, starts_on, notes, is_active, created_at, updated_at)
            VALUES (@Id, @Name, @CategoryId, @AmountCents, @CashBoxId, @EnvelopeId, @Frequency,
                    @DueDay, @StartsOn, @Notes, @IsActive, @CreatedAt, @UpdatedAt)
            """, ToParameters(recurring), cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<RecurringExpense?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<RecurringExpense>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM recurring_expenses r {Joins} WHERE r.id = @id",
            new { id }, cancellationToken: cancellationToken));
    }

    public async Task<RecurringExpense?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<RecurringExpense>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM recurring_expenses r {Joins} WHERE r.name = @name COLLATE NOCASE",
            new { name }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<RecurringExpense>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<RecurringExpense>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM recurring_expenses r {Joins} ORDER BY r.is_active DESC, r.name COLLATE NOCASE",
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(RecurringExpense recurring, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE recurring_expenses SET
                name = @Name,
                category_id = @CategoryId,
                amount_cents = @AmountCents,
                cash_box_id = @CashBoxId,
                envelope_id = @EnvelopeId,
                frequency = @Frequency,
                due_day = @DueDay,
                starts_on = @StartsOn,
                notes = @Notes,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, ToParameters(recurring), cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<IReadOnlyList<TakenPeriod>> GetTakenPeriodsAsync(Guid? recurringExpenseId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<TakenPeriod>(new CommandDefinition("""
            SELECT recurring_expense_id, period_start
            FROM expenses
            WHERE recurring_expense_id IS NOT NULL
              AND status = 'Confirmed'
              AND (@recurringExpenseId IS NULL OR recurring_expense_id = @recurringExpenseId)
            UNION
            SELECT recurring_expense_id, period_start
            FROM recurring_expense_skips
            WHERE @recurringExpenseId IS NULL OR recurring_expense_id = @recurringExpenseId
            """, new { recurringExpenseId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task SkipAsync(SkipRecurringExpense skip, DateTime now, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT OR IGNORE INTO recurring_expense_skips (recurring_expense_id, period_start, created_at)
            VALUES (@recurringExpenseId, @periodStart, @now)
            """, new { recurringExpenseId = skip.RecurringExpenseId, periodStart = skip.PeriodStart, now },
            cancellationToken: cancellationToken));
    }

    private static object ToParameters(RecurringExpense recurring) => new
    {
        recurring.Id,
        recurring.Name,
        recurring.CategoryId,
        recurring.AmountCents,
        recurring.CashBoxId,
        recurring.EnvelopeId,
        Frequency = recurring.Frequency.ToString(),
        recurring.DueDay,
        recurring.StartsOn,
        recurring.Notes,
        recurring.IsActive,
        recurring.CreatedAt,
        recurring.UpdatedAt
    };
}
