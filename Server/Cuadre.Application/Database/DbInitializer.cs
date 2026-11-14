using DbUp;
using Microsoft.Data.Sqlite;

namespace Cuadre.Application.Database;

/// <summary>
/// Creates the SQLite file if missing and applies embedded SQL scripts (Database/Scripts/*.sql)
/// in name order. DbUp journals applied scripts in "SchemaVersions"; never edit an applied script.
/// </summary>
public class DbInitializer
{
    private readonly string _connectionString;

    public DbInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>DbUp's journal table: one row per applied script, named after the embedded resource.</summary>
    public const string JournalTable = "SchemaVersions";

    /// <summary>
    /// Every migration this version of the app knows. A backup whose last migration is not here comes from a NEWER
    /// version: restoring it would leave a database this app does not understand.
    /// </summary>
    public static IReadOnlySet<string> KnownScripts { get; } = typeof(DbInitializer).Assembly
        .GetManifestResourceNames()
        .Where(name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>The database file (absolute path).</summary>
    public string DatabaseFile => Path.GetFullPath(new SqliteConnectionStringBuilder(_connectionString).DataSource);

    public string ConnectionString => _connectionString;

    /// <summary>True when the file exists and this version has migrations it still lacks (D-57: back it up first).</summary>
    public bool IsUpgradeRequired() => File.Exists(DatabaseFile) && BuildUpgrader().IsUpgradeRequired();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists();
        await EnableWriteAheadLogAsync(cancellationToken);

        var upgrader = BuildUpgrader();

        if (!upgrader.IsUpgradeRequired())
        {
            return;
        }

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException("Database upgrade failed", result.Error);
        }
    }

    private DbUp.Engine.UpgradeEngine BuildUpgrader() => DeployChanges.To.SqliteDatabase(_connectionString)
        .WithScriptsEmbeddedInAssembly(typeof(DbInitializer).Assembly)
        .WithTransactionPerScript()
        .LogToConsole()
        .Build();

    private void EnsureDirectoryExists()
    {
        var dataSource = new SqliteConnectionStringBuilder(_connectionString).DataSource;
        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// WAL is persistent (stored in the file), so it is set once here and not per connection.
    /// It lets the backup read the database while the app keeps writing.
    /// </summary>
    private async Task EnableWriteAheadLogAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
