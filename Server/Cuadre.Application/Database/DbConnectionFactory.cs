using System.Data;
using Microsoft.Data.Sqlite;

namespace Cuadre.Application.Database;

public interface IDbConnectionFactory
{
    /// <summary>Returns an OPEN connection. Callers own it: always `using var connection = ...`.</summary>
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Per-connection settings (SQLite does not persist them in the file):
        // - foreign_keys: SQLite ignores FOREIGN KEY constraints unless this is ON.
        // - synchronous FULL: with WAL, a power cut cannot lose a committed transaction (RNF-05).
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA synchronous = FULL;";
        await pragma.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}
