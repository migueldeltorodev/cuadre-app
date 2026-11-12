using Cuadre.Application.Database;
using Cuadre.Application.Models;
using Dapper;

namespace Cuadre.Application.Repositories;

public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Location>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Where purchases land when none is chosen: the first active Storage location (Almacén).</summary>
    Task<Location?> GetDefaultStorageAsync(CancellationToken cancellationToken = default);
}

/// <summary>Read-only in slice 3. Creating and renaming locations (RF-INV-01) arrives with inventory.</summary>
public class LocationRepository : ILocationRepository
{
    private const string SelectColumns = "id, name, kind, is_active, created_at, updated_at";

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public LocationRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Location>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM locations WHERE id = @id", new { id }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Location>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Location>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM locations ORDER BY is_active DESC, kind, created_at",
            cancellationToken: cancellationToken));
    }

    public async Task<Location?> GetDefaultStorageAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<Location>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM locations WHERE kind = 'Storage' AND is_active = 1 ORDER BY created_at LIMIT 1",
            cancellationToken: cancellationToken));
    }
}
