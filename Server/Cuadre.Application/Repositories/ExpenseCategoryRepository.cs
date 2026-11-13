using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>Few rows in a lifetime: no paging. Never deleted, only deactivated.</summary>
public interface IExpenseCategoryRepository
{
    Task<bool> CreateAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<ExpenseCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Ignoring case.</summary>
    Task<ExpenseCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Active first, then by name; each with its confirmed expense count.</summary>
    Task<IEnumerable<ExpenseCategory>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
}

public class ExpenseCategoryRepository : IExpenseCategoryRepository
{
    private const string SelectColumns = """
        c.id, c.name, c.is_active, c.created_at, c.updated_at,
        (SELECT COUNT(*) FROM expenses x WHERE x.category_id = c.id AND x.status = 'Confirmed') AS expense_count
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public ExpenseCategoryRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO expense_categories (id, name, is_active, created_at, updated_at)
            VALUES (@Id, @Name, @IsActive, @CreatedAt, @UpdatedAt)
            """, new { category.Id, category.Name, category.IsActive, category.CreatedAt, category.UpdatedAt },
            cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<ExpenseCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ExpenseCategory>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM expense_categories c WHERE c.id = @id",
            new { id }, cancellationToken: cancellationToken));
    }

    public async Task<ExpenseCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ExpenseCategory>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM expense_categories c WHERE c.name = @name COLLATE NOCASE",
            new { name }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<ExpenseCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<ExpenseCategory>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM expense_categories c ORDER BY c.is_active DESC, c.name COLLATE NOCASE",
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE expense_categories SET name = @Name, is_active = @IsActive, updated_at = @UpdatedAt WHERE id = @Id
            """, new { category.Id, category.Name, category.IsActive, category.UpdatedAt },
            cancellationToken: cancellationToken));
        return result > 0;
    }
}
