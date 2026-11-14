using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Cuadre.Application.Database;

/// <summary>
/// The SQLite side of backups. Copying cuadre.db with the Explorer is NOT safe: in WAL mode the latest changes may
/// still be in cuadre.db-wal. These use SQLite itself, so the copy is always whole and consistent.
/// Copies are opened with Pooling=False: a pooled connection would keep the file locked and it could not be zipped
/// or deleted.
/// </summary>
internal static class SqliteBackup
{
    /// <summary>VACUUM INTO: a complete, compact copy of the live database, safe while the app keeps working.</summary>
    public static async Task CopyToAsync(string liveConnectionString, string targetFile,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(liveConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $target;";
        command.Parameters.AddWithValue("$target", targetFile);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Opens a copy read-only and checks it: integrity_check must answer "ok". Returns its last migration
    /// (null when it has no DbUp journal: then it is not a Cuadre database).
    /// </summary>
    public static async Task<(bool IsHealthy, string? SchemaVersion)> InspectAsync(string file,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqliteConnection(ReadOnly(file));
            await connection.OpenAsync(cancellationToken);

            await using var check = connection.CreateCommand();
            check.CommandText = "PRAGMA integrity_check;";
            var verdict = await check.ExecuteScalarAsync(cancellationToken) as string;
            if (!string.Equals(verdict, "ok", StringComparison.Ordinal))
            {
                return (false, null);
            }

            await using var journal = connection.CreateCommand();
            journal.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $journal;";
            journal.Parameters.AddWithValue("$journal", DbInitializer.JournalTable);
            if (Convert.ToInt64(await journal.ExecuteScalarAsync(cancellationToken)) == 0)
            {
                return (true, null);
            }

            // Script names are zero-padded (Script0009 - ...), so the greatest name is the last migration.
            await using var version = connection.CreateCommand();
            version.CommandText = $"SELECT MAX(ScriptName) FROM {DbInitializer.JournalTable};";
            return (true, await version.ExecuteScalarAsync(cancellationToken) as string);
        }
        catch (SqliteException)
        {
            // Not a database at all ("file is not a database"): as unhealthy as a corrupt one.
            return (false, null);
        }
    }

    /// <summary>
    /// RF-DAT-05: copies a verified backup INTO the live database with SQLite's online backup. No file is deleted or
    /// swapped while the app is running, and the other connections see the new data on their next query.
    /// </summary>
    public static async Task RestoreIntoAsync(string sourceFile, string liveConnectionString,
        CancellationToken cancellationToken)
    {
        await using var source = new SqliteConnection(ReadOnly(sourceFile));
        await using var destination = new SqliteConnection(liveConnectionString);
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    public static async Task<string> Sha256Async(string file, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(file);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    private static string ReadOnly(string file) => new SqliteConnectionStringBuilder
    {
        DataSource = file,
        Mode = SqliteOpenMode.ReadOnly,
        Pooling = false
    }.ToString();
}
