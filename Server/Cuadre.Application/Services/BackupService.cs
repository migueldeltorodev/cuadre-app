using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Cuadre.Application.Common;
using Cuadre.Application.Database;
using Cuadre.Application.Errors;
using Cuadre.Application.Models;
using FluentResults;
using FluentValidation;
using Microsoft.Data.Sqlite;

namespace Cuadre.Application.Services;

/// <summary>
/// RF-DAT-01..03, 05, 06. One backup = the database copied by SQLite (VACUUM INTO), verified (integrity_check),
/// zipped with a manifest (hash, migration) into the local folder, then copied to the pendrive and checked there too.
/// Each folder is rotated after a copy. Backups and restores never overlap (one gate).
/// </summary>
public interface IBackupService
{
    Task<BackupStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<Result<BackupResult>> CreateAsync(BackupReason reason, CancellationToken cancellationToken = default);

    /// <summary>D-57: at startup, a safety copy before a new version migrates the database. No-op otherwise.</summary>
    Task CreateBeforeUpdateIfNeededAsync(CancellationToken cancellationToken = default);

    Task<Result<BackupSettings>> UpdateSettingsAsync(BackupSettings settings,
        CancellationToken cancellationToken = default);

    IReadOnlyList<RemovableDrive> GetRemovableDrives();

    Task<Result<RestoreResult>> RestoreAsync(BackupLocation location, string fileName,
        CancellationToken cancellationToken = default);
}

public class BackupService : IBackupService
{
    private const string WorkFolder = ".tmp";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Singleton service: the gate is shared by the closing, the window closing, the button and the restore.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly BackupPaths _paths;
    private readonly BackupStateStore _state;
    private readonly DbInitializer _database;
    private readonly DatabaseSeeder _seeder;
    private readonly IValidator<BackupSettings> _validator;
    private readonly TimeProvider _clock;

    public BackupService(BackupPaths paths, BackupStateStore state, DbInitializer database, DatabaseSeeder seeder,
        IValidator<BackupSettings> validator, TimeProvider clock)
    {
        _paths = paths;
        _state = state;
        _database = database;
        _seeder = seeder;
        _validator = validator;
        _clock = clock;
    }

    // ---------- Status ----------

    public Task<BackupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var state = _state.Read();
        var external = state.Settings.ExternalDirectory;
        var externalAvailable = !string.IsNullOrWhiteSpace(external) && Directory.Exists(external);

        var backups = List(_paths.LocalDirectory, BackupLocation.Local)
            .Concat(externalAvailable ? List(external!, BackupLocation.External) : [])
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ThenBy(entry => entry.Location)
            .ToList();

        return Task.FromResult(new BackupStatus(
            state.Settings,
            externalAvailable,
            state.LastLocal,
            state.LastExternal,
            state.LastProblem,
            BackupAlerts.Evaluate(state, _clock.Today(), _clock.LocalTimeZone),
            backups));
    }

    // ---------- Create ----------

    public async Task<Result<BackupResult>> CreateAsync(BackupReason reason,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await CreateUnderGateAsync(reason, copyToExternal: true, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CreateBeforeUpdateIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (_database.IsUpgradeRequired())
        {
            // A failure is recorded in backup.json and shown on the home screen; the update goes on anyway
            // (each migration runs in its own transaction) rather than leaving the shop without its app.
            await CreateAsync(BackupReason.BeforeUpdate, cancellationToken);
        }
    }

    private async Task<Result<BackupResult>> CreateUnderGateAsync(BackupReason reason, bool copyToExternal,
        CancellationToken cancellationToken)
    {
        var nowUtc = _clock.UtcNow();
        var localDirectory = _paths.LocalDirectory;
        var work = Path.Combine(localDirectory, WorkFolder);
        var copy = Path.Combine(work, $"{Guid.NewGuid():N}.db");
        string? zipTemp = null;

        try
        {
            Directory.CreateDirectory(work);
            var fileName = UniqueName(localDirectory, _clock.GetLocalNow().DateTime, reason);

            // 1. The copy, made by SQLite itself, and checked before anything else happens.
            await SqliteBackup.CopyToAsync(_database.ConnectionString, copy, cancellationToken);
            var (isHealthy, schemaVersion) = await SqliteBackup.InspectAsync(copy, cancellationToken);
            if (!isHealthy)
            {
                return Fail("La copia de la base salió dañada y no se guardó. Probá de nuevo.", nowUtc);
            }

            // 2. Zipped with its manifest; only a finished zip gets its real name.
            var manifest = new BackupManifest
            {
                CreatedAtUtc = nowUtc,
                Reason = reason,
                SchemaVersion = schemaVersion,
                DatabaseSha256 = await SqliteBackup.Sha256Async(copy, cancellationToken),
                DatabaseBytes = new FileInfo(copy).Length
            };
            zipTemp = Path.Combine(work, fileName + ".tmp");
            WriteZip(zipTemp, copy, manifest);
            var localFile = Path.Combine(localDirectory, fileName);
            File.Move(zipTemp, localFile);
            zipTemp = null;

            _state.Update(state => state.LastLocal = new BackupMark(nowUtc, fileName));
            var entry = ToEntry(localFile, BackupLocation.Local)!;   // before the rotation, which deletes files
            Rotate(localDirectory, BackupLocation.Local);

            // 3. The pendrive. Missing is normal (it is not always plugged in); a failed copy is a problem.
            var (outcome, message) = copyToExternal
                ? await CopyToExternalAsync(localFile, fileName, nowUtc, cancellationToken)
                : (ExternalCopyOutcome.NotConfigured, null);

            if (outcome != ExternalCopyOutcome.Failed)
            {
                _state.Update(state => state.LastProblem = null);
            }

            return Result.Ok(new BackupResult(entry, outcome, message));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            return Fail($"No se pudo hacer el respaldo: {ex.Message}", nowUtc);
        }
        finally
        {
            TryDelete(copy);
            if (zipTemp is not null)
            {
                TryDelete(zipTemp);
            }
        }
    }

    private async Task<(ExternalCopyOutcome, string?)> CopyToExternalAsync(string localFile, string fileName,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        var directory = _state.Read().Settings.ExternalDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return (ExternalCopyOutcome.NotConfigured, null);
        }

        if (!Directory.Exists(directory))
        {
            return (ExternalCopyOutcome.Unavailable,
                $"No encontré {directory}. Conectá el pendrive y tocá \"Respaldar ahora\".");
        }

        var temp = Path.Combine(directory, fileName + ".tmp");
        try
        {
            File.Copy(localFile, temp, overwrite: true);

            // A pendrive can write garbage without an error: compare the copy with the original.
            if (await SqliteBackup.Sha256Async(temp, cancellationToken)
                != await SqliteBackup.Sha256Async(localFile, cancellationToken))
            {
                TryDelete(temp);
                return Problem("La copia en el pendrive salió distinta del original. Probá con otro pendrive.");
            }

            File.Move(temp, Path.Combine(directory, fileName), overwrite: true);
            _state.Update(state => state.LastExternal = new BackupMark(nowUtc, fileName));
            Rotate(directory, BackupLocation.External);
            return (ExternalCopyOutcome.Copied, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            return Problem($"No se pudo copiar al pendrive: {ex.Message}");
        }

        (ExternalCopyOutcome, string?) Problem(string message)
        {
            _state.Update(state => state.LastProblem = new BackupProblem(nowUtc, message));
            return (ExternalCopyOutcome.Failed, message);
        }
    }

    private Result<BackupResult> Fail(string message, DateTime nowUtc)
    {
        _state.Update(state => state.LastProblem = new BackupProblem(nowUtc, message));
        return Result.Fail(new BusinessRuleError(message));
    }

    private static void WriteZip(string zipFile, string databaseFile, BackupManifest manifest)
    {
        using var zip = ZipFile.Open(zipFile, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(databaseFile, BackupManifest.DatabaseEntryName, CompressionLevel.Optimal);
        using var stream = zip.CreateEntry(BackupManifest.EntryName).Open();
        JsonSerializer.Serialize(stream, manifest, Json);
    }

    private static string UniqueName(string directory, DateTime local, BackupReason reason)
    {
        for (var sequence = 1; ; sequence++)
        {
            var name = BackupNaming.Format(local, reason, sequence);
            if (!File.Exists(Path.Combine(directory, name)))
            {
                return name;
            }
        }
    }

    // ---------- Settings ----------

    public async Task<Result<BackupSettings>> UpdateSettingsAsync(BackupSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateOrThrowAsync(settings, cancellationToken);

        string? directory = null;
        if (!string.IsNullOrWhiteSpace(settings.ExternalDirectory))
        {
            var checkedDirectory = PrepareExternalDirectory(settings.ExternalDirectory.Trim());
            if (checkedDirectory.IsFailed)
            {
                return checkedDirectory.ToResult();
            }

            directory = checkedDirectory.Value;
        }

        var saved = _state.Update(state =>
        {
            // Another folder (another pendrive) has none of the copies: the alert must start over.
            if (!string.Equals(state.Settings.ExternalDirectory, directory, StringComparison.OrdinalIgnoreCase))
            {
                state.LastExternal = null;
            }

            state.Settings = new BackupSettings { ExternalDirectory = directory, AlertAfterDays = settings.AlertAfterDays };
        });
        return Result.Ok(saved.Settings);
    }

    /// <summary>Creates the folder when the drive is there (E:\ plugged in → E:\Cuadre respaldos) and tries a write.</summary>
    private Result<string> PrepareExternalDirectory(string directory)
    {
        var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        if (full.Length < 3)
        {
            full += Path.DirectorySeparatorChar;   // "E:" alone means the current folder of E:, not its root
        }

        var local = Path.GetFullPath(_paths.LocalDirectory).TrimEnd(Path.DirectorySeparatorChar);
        // The folder itself or one inside it ("backups2" next to it is fine).
        if (full.Equals(local, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(local + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail(new BusinessRuleError(
                "Esa es la carpeta de los respaldos de la PC. Elegí una carpeta del pendrive."));
        }

        try
        {
            if (!Directory.Exists(full))
            {
                var root = Path.GetPathRoot(full);
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                {
                    return Result.Fail(new BusinessRuleError(
                        $"No encontré la unidad {root}. ¿Está conectado el pendrive?"));
                }

                Directory.CreateDirectory(full);
            }

            var probe = Path.Combine(full, ".cuadre-prueba");
            File.WriteAllText(probe, "Cuadre");
            File.Delete(probe);
            return Result.Ok(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Fail(new BusinessRuleError(
                $"No se puede escribir en {full}: {ex.Message} Revisá que el pendrive no esté protegido contra escritura."));
        }
    }

    public IReadOnlyList<RemovableDrive> GetRemovableDrives()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(drive => drive is { DriveType: DriveType.Removable, IsReady: true })
                .Select(drive => new RemovableDrive(drive.RootDirectory.FullName, drive.VolumeLabel,
                    drive.TotalSize, drive.AvailableFreeSpace))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // ---------- Restore ----------

    public async Task<Result<RestoreResult>> RestoreAsync(BackupLocation location, string fileName,
        CancellationToken cancellationToken = default)
    {
        // The strict name pattern is also the guard against paths like "..\..\cuadre.db".
        if (!BackupNaming.TryParse(fileName, out _, out _))
        {
            return Result.Fail(new NotFoundError("No encontré ese respaldo."));
        }

        var directory = location == BackupLocation.Local
            ? _paths.LocalDirectory
            : _state.Read().Settings.ExternalDirectory;
        if (location == BackupLocation.External && (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)))
        {
            return Result.Fail(new BusinessRuleError("No encontré la carpeta del pendrive. ¿Está conectado?"));
        }

        var file = Path.Combine(directory!, fileName);
        if (!File.Exists(file))
        {
            return Result.Fail(new NotFoundError("No encontré ese respaldo."));
        }

        await _gate.WaitAsync(cancellationToken);
        var work = Path.Combine(_paths.LocalDirectory, WorkFolder);
        var extracted = Path.Combine(work, $"{Guid.NewGuid():N}.db");
        try
        {
            Directory.CreateDirectory(work);

            // 1. Everything is checked BEFORE the current data is touched.
            var read = ReadBackup(file, extracted);
            if (read.IsFailed)
            {
                return read.ToResult();
            }

            var manifest = read.Value;
            if (await SqliteBackup.Sha256Async(extracted, cancellationToken) != manifest.DatabaseSha256)
            {
                return Result.Fail(new BusinessRuleError(
                    "Ese respaldo está dañado (no coincide su huella). No se restauró nada."));
            }

            var (isHealthy, schemaVersion) = await SqliteBackup.InspectAsync(extracted, cancellationToken);
            if (!isHealthy || schemaVersion is null)
            {
                return Result.Fail(new BusinessRuleError(
                    "Ese respaldo está dañado o no es de Cuadre. No se restauró nada."));
            }

            if (!DbInitializer.KnownScripts.Contains(schemaVersion))
            {
                return Result.Fail(new BusinessRuleError(
                    "Ese respaldo es de una versión más nueva de Cuadre. Actualizá la app y probá de nuevo."));
            }

            // Read before the safety copy: its rotation must not leave us describing a file that is gone.
            var restored = ToEntry(file, location)!;

            // 2. A copy of what is here now, in case the owner changes their mind.
            var safety = await CreateUnderGateAsync(BackupReason.BeforeRestore, copyToExternal: false,
                cancellationToken);
            if (safety.IsFailed)
            {
                return Result.Fail(new BusinessRuleError(
                    "No se pudo guardar una copia de lo que hay ahora, así que no se restauró nada."));
            }

            // 3. In: SQLite copies it into the open database. An older copy gets the missing migrations.
            await SqliteBackup.RestoreIntoAsync(extracted, _database.ConnectionString, cancellationToken);
            await _database.InitializeAsync(cancellationToken);
            await _seeder.SeedAsync(cancellationToken);

            return Result.Ok(new RestoreResult(restored, manifest.CreatedAtUtc, safety.Value.Backup));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            return Result.Fail(new BusinessRuleError($"No se pudo restaurar: {ex.Message}"));
        }
        finally
        {
            TryDelete(extracted);
            _gate.Release();
        }
    }

    private static Result<BackupManifest> ReadBackup(string file, string extractTo)
    {
        try
        {
            using var zip = ZipFile.OpenRead(file);
            var manifestEntry = zip.GetEntry(BackupManifest.EntryName);
            var databaseEntry = zip.GetEntry(BackupManifest.DatabaseEntryName);
            if (manifestEntry is null || databaseEntry is null)
            {
                return Result.Fail(new BusinessRuleError("Ese archivo no es un respaldo de Cuadre."));
            }

            BackupManifest? manifest;
            using (var stream = manifestEntry.Open())
            {
                manifest = JsonSerializer.Deserialize<BackupManifest>(stream, Json);
            }

            if (manifest is null || manifest.App != "Cuadre")
            {
                return Result.Fail(new BusinessRuleError("Ese archivo no es un respaldo de Cuadre."));
            }

            if (manifest.Format > BackupManifest.CurrentFormat)
            {
                return Result.Fail(new BusinessRuleError(
                    "Ese respaldo es de una versión más nueva de Cuadre. Actualizá la app y probá de nuevo."));
            }

            databaseEntry.ExtractToFile(extractTo, overwrite: true);
            return Result.Ok(manifest);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException)
        {
            return Result.Fail(new BusinessRuleError("Ese respaldo está dañado y no se puede abrir."));
        }
    }

    // ---------- Files ----------

    private IEnumerable<BackupEntry> List(string directory, BackupLocation location)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, BackupNaming.SearchPattern)
            .Select(file => ToEntry(file, location))
            .OfType<BackupEntry>()
            .ToList();
    }

    private BackupEntry? ToEntry(string file, BackupLocation location)
    {
        var name = Path.GetFileName(file);
        if (!BackupNaming.TryParse(name, out var local, out var reason))
        {
            return null;
        }

        // A time inside a daylight-saving jump does not exist: move it past the jump.
        var zone = _clock.LocalTimeZone;
        var valid = zone.IsInvalidTime(local) ? local.AddHours(1) : local;
        return new BackupEntry(name, location, local, TimeZoneInfo.ConvertTimeToUtc(valid, zone), reason,
            new FileInfo(file).Length);
    }

    private void Rotate(string directory, BackupLocation location)
    {
        foreach (var old in BackupRotation.SelectToDelete(List(directory, location)))
        {
            TryDelete(Path.Combine(directory, old.FileName));
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temp file is harmless; the next run cleans or ignores it.
        }
    }
}

// ---------- Automatic backups: requested by the closing, made by a background worker (Api) ----------

/// <summary>Asks for a backup without waiting for it: the closing answers at once, the copy follows.</summary>
public interface IBackupScheduler
{
    void Request(BackupReason reason);
}

/// <summary>The queue between the requests and the worker. One reader: backups are made one after another.</summary>
public sealed class BackupQueue : IBackupScheduler
{
    private readonly Channel<BackupReason> _channel =
        Channel.CreateUnbounded<BackupReason>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<BackupReason> Reader => _channel.Reader;

    public void Request(BackupReason reason) => _channel.Writer.TryWrite(reason);

    /// <summary>No more requests: the worker finishes what is queued and stops.</summary>
    public void Complete() => _channel.Writer.TryComplete();
}
