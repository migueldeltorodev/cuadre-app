using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

/// <summary>D-62. A handful of rows in a lifetime: no paging. Never deleted, only deactivated.</summary>
public interface ITaxRepository
{
    Task<bool> CreateAsync(Tax tax, CancellationToken cancellationToken = default);
    Task<Tax?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Ignoring case.</summary>
    Task<Tax?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Active first, then by name.</summary>
    Task<IEnumerable<Tax>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Tax tax, CancellationToken cancellationToken = default);
}

public class TaxRepository : ITaxRepository
{
    private const string SelectColumns = "id, name, rate_bps, base, is_active, created_at, updated_at";

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public TaxRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(Tax tax, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO taxes (id, name, rate_bps, base, is_active, created_at, updated_at)
            VALUES (@Id, @Name, @RateBps, @Base, @IsActive, @CreatedAt, @UpdatedAt)
            """, new
        {
            tax.Id,
            tax.Name,
            tax.RateBps,
            Base = tax.Base.ToString(),
            tax.IsActive,
            tax.CreatedAt,
            tax.UpdatedAt
        }, cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<Tax?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Tax>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM taxes WHERE id = @id", new { id }, cancellationToken: cancellationToken));
    }

    public async Task<Tax?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        // The column is COLLATE NOCASE, so "onat" finds "ONAT".
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Tax>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM taxes WHERE name = @name", new { name },
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Tax>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Tax>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM taxes ORDER BY is_active DESC, name",
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(Tax tax, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE taxes SET
                name = @Name,
                rate_bps = @RateBps,
                base = @Base,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new
        {
            tax.Id,
            tax.Name,
            tax.RateBps,
            Base = tax.Base.ToString(),
            tax.IsActive,
            tax.UpdatedAt
        }, cancellationToken: cancellationToken));
        return result > 0;
    }
}
