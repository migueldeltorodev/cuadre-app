using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface ILocationRepository
{
    Task<bool> CreateAsync(Location location, CancellationToken cancellationToken = default);
    Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Location?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Location>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Only name and active flag: the kind is immutable (the daily close relies on the Counter).</summary>
    Task<bool> UpdateAsync(Location location, CancellationToken cancellationToken = default);

    /// <summary>Where purchases land when none is chosen: the first active Storage location (Almacén).</summary>
    Task<Location?> GetDefaultStorageAsync(CancellationToken cancellationToken = default);

    /// <summary>True while any product has a non-zero balance there (positive or negative).</summary>
    Task<bool> HasStockAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountActiveOfKindAsync(LocationKind kind, CancellationToken cancellationToken = default);
}

/// <summary>No delete: locations hold movements, so they are deactivated instead (RF-INV-01).</summary>
public class LocationRepository : ILocationRepository
{
    private const string SelectColumns = "id, name, kind, is_active, created_at, updated_at";

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public LocationRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(Location location, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO locations (id, name, kind, is_active, created_at, updated_at)
            VALUES (@Id, @Name, @Kind, @IsActive, @CreatedAt, @UpdatedAt)
            """, new
        {
            location.Id,
            location.Name,
            Kind = location.Kind.ToString(),
            location.IsActive,
            location.CreatedAt,
            location.UpdatedAt
        }, cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Location>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM locations WHERE id = @id", new { id }, cancellationToken: cancellationToken));
    }

    public async Task<Location?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        // The column is COLLATE NOCASE, so "almacén" finds "Almacén".
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Location>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM locations WHERE name = @name",
            new { name }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Location>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Location>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM locations ORDER BY is_active DESC, kind, created_at",
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(Location location, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE locations SET
                name = @Name,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, new { location.Id, location.Name, location.IsActive, location.UpdatedAt },
            cancellationToken: cancellationToken));
        return result > 0;
    }

    public async Task<Location?> GetDefaultStorageAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<Location>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM locations WHERE kind = 'Storage' AND is_active = 1 ORDER BY created_at LIMIT 1",
            cancellationToken: cancellationToken));
    }

    public async Task<bool> HasStockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS (
                SELECT 1 FROM stock_movements
                WHERE location_id = @id
                GROUP BY product_id
                HAVING SUM(quantity_milli) <> 0)
            """, new { id }, cancellationToken: cancellationToken));
    }

    public async Task<int> CountActiveOfKindAsync(LocationKind kind, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM locations WHERE kind = @kind AND is_active = 1",
            new { kind = kind.ToString() }, cancellationToken: cancellationToken));
    }
}
