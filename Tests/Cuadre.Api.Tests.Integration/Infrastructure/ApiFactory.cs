using Cuadre.Api;
using Cuadre.Application.Database;
using Cuadre.Application.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Cuadre.Api.Tests.Integration.Infrastructure;

/// <summary>
/// ONE in-memory API host + ONE real SQLite file (in a temp folder) for the whole test run,
/// shared through ApiCollection. Startup runs DbUp against that file.
/// Replaces the skill's Testcontainers/PostgreSQL + Respawn: no Docker needed on the PC.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<IApiMarker>, IAsyncLifetime
{
    private const string DbUpJournalTable = "SchemaVersions";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "cuadre-tests", Guid.NewGuid().ToString("N"));

    /// <summary>The app's only clock. Tests move it to put "today" wherever they need.</summary>
    public TestClock Clock { get; } = new();

    /// <summary>What the closings asked to back up (the real queue and its worker never run in tests).</summary>
    public RecordingBackupScheduler BackupRequests { get; } = new();

    /// <summary>App:DataDirectory: the local backups folder and backup.json live here.</summary>
    public string DataDirectory => _directory;

    public string BackupsDirectory => Path.Combine(_directory, "backups");

    /// <summary>A folder that plays the pendrive (a real removable drive is not something a test can count on).</summary>
    public string PendriveDirectory => Path.Combine(_directory, "pendrive");

    private string BackupStateFile => Path.Combine(_directory, "backup.json");

    // Pooling=False: no connection survives the run, so the temp folder can be deleted at the end.
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "cuadre-tests.db")};Pooling=False";

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        // Touching Services builds and starts the host => Program.cs runs DbInitializer (DbUp) on the file.
        _ = Services;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Back to "fresh install": wipe every row, re-seed the factory defaults, reset the clock.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        Clock.Now = TestClock.Default;
        BackupRequests.Clear();
        DeleteBackupFiles();
        await WipeAllRowsAsync();
        await Services.GetRequiredService<DatabaseSeeder>().SeedAsync();
    }

    /// <summary>The backups, the pendrive and backup.json (D-56: outside the database, so the wipe misses them).</summary>
    private void DeleteBackupFiles()
    {
        foreach (var directory in new[] { BackupsDirectory, PendriveDirectory })
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        File.Delete(BackupStateFile);
    }

    /// <summary>
    /// Deletes every row of every table except the DbUp journal (wiping it would re-run every script).
    /// Foreign keys are switched off for the wipe so table order does not matter.
    /// </summary>
    private async Task WipeAllRowsAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var list = connection.CreateCommand())
        {
            list.CommandText = """
                SELECT name FROM sqlite_master
                WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name <> $journal COLLATE NOCASE
                """;
            list.Parameters.AddWithValue("$journal", DbUpJournalTable);
            await using var reader = await list.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        if (tables.Count == 0)
        {
            return;
        }

        await using var wipe = connection.CreateCommand();
        wipe.CommandText = "PRAGMA foreign_keys = OFF;"
                           + string.Concat(tables.Select(t => $"DELETE FROM \"{t}\";"))
                           + "PRAGMA foreign_keys = ON;";
        await wipe.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Read eagerly by Program.cs; UseSetting is visible during WebApplication.CreateBuilder.
        builder.UseSetting("Database:ConnectionString", ConnectionString);
        builder.UseSetting("App:DataDirectory", _directory);   // logs, backups... also go to the temp folder
        builder.UseSetting("Desktop:LaunchWindow", "false");   // never open Edge from a test
        builder.UseSetting("Backup:OnExit", "false");          // no backup when the test host stops

        // Registered after AddApplication's TryAddSingleton(...), so these win.
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<IBackupScheduler>(BackupRequests);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A log file may still be held for a moment; the OS cleans the temp folder eventually.
        }
    }
}
