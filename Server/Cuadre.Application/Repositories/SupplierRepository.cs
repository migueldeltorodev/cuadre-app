using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface ISupplierRepository
{
    Task<bool> CreateAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Supplier?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Supplier>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default);
}

/// <summary>No delete: purchases (slice 3) will reference suppliers. They are deactivated instead.</summary>
public class SupplierRepository : ISupplierRepository
{
    private const string SelectColumns = "id, name, contact_name, phone, notes, is_active, created_at, updated_at";

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public SupplierRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO suppliers (id, name, contact_name, phone, notes, is_active, created_at, updated_at)
            VALUES (@Id, @Name, @ContactName, @Phone, @Notes, @IsActive, @CreatedAt, @UpdatedAt)
            """, supplier, cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Supplier>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM suppliers WHERE id = @id",
            new { id }, cancellationToken: cancellationToken));
    }

    public async Task<Supplier?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Supplier>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM suppliers WHERE name = @name",
            new { name }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Supplier>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Supplier>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM suppliers ORDER BY is_active DESC, name COLLATE NOCASE",
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE suppliers SET
                name = @Name,
                contact_name = @ContactName,
                phone = @Phone,
                notes = @Notes,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, supplier, cancellationToken: cancellationToken));
        return result > 0;
    }
}
