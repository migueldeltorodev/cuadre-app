using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface ICategoryRepository
{
    Task<bool> CreateAsync(Category category, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Same name under the same parent (null parent = top level), ignoring case.</summary>
    Task<Category?> GetByNameAsync(Guid? parentId, string name, CancellationToken cancellationToken = default);

    Task<Category?> GetByPrefixAsync(string codePrefix, CancellationToken cancellationToken = default);

    /// <summary>Flat list, top-level first; each with its product count.</summary>
    Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True if it has subcategories or any product points to it.</summary>
    Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Categories CAN be deleted (unlike cash boxes or products), but only while nothing uses them:
/// a typo in a new category should not stay forever.
/// </summary>
public class CategoryRepository : ICategoryRepository
{
    // A product points to a category through category_id OR to a subcategory through subcategory_id,
    // so one OR counts the right thing for both levels.
    private const string SelectColumns = """
        c.id, c.parent_id, c.name, c.code_prefix, c.created_at, c.updated_at,
        (SELECT COUNT(*) FROM products p WHERE p.category_id = c.id OR p.subcategory_id = c.id) AS product_count
        """;

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public CategoryRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(Category category, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO categories (id, parent_id, name, code_prefix, created_at, updated_at)
            VALUES (@Id, @ParentId, @Name, @CodePrefix, @CreatedAt, @UpdatedAt)
            """, category, cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Category>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM categories c WHERE c.id = @id",
            new { id }, cancellationToken: cancellationToken));
    }

    public async Task<Category?> GetByNameAsync(Guid? parentId, string name,
        CancellationToken cancellationToken = default)
    {
        // "parent_id = NULL" is never true in SQL: IS compares NULLs as equal.
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<Category>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM categories c WHERE c.parent_id IS @parentId AND c.name = @name COLLATE NOCASE",
            new { parentId, name }, cancellationToken: cancellationToken));
    }

    public async Task<Category?> GetByPrefixAsync(string codePrefix, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Category>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM categories c WHERE c.code_prefix = @codePrefix",
            new { codePrefix }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Category>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM categories c ORDER BY c.parent_id IS NOT NULL, c.name COLLATE NOCASE",
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        // parent_id is deliberately NOT updatable: a category never changes level.
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE categories SET
                name = @Name,
                code_prefix = @CodePrefix,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new { category.Id, category.Name, category.CodePrefix, category.UpdatedAt },
            cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM categories WHERE id = @id", new { id }, cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM categories WHERE parent_id = @id)
                OR EXISTS(SELECT 1 FROM products WHERE category_id = @id OR subcategory_id = @id)
            """, new { id }, cancellationToken: cancellationToken));
    }
}
