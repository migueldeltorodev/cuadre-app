using System.Text.Json;
using System.Text.Json.Serialization;
using Cuadre.Application.Models;

namespace Cuadre.Application.Services;

/// <summary>Where the backups live on this PC (registered by the Api from AppPaths).</summary>
public sealed record BackupPaths(string LocalDirectory, string StateFile);

/// <summary>
/// D-56: backup.json, next to the database and never inside it. Read on every call (it is tiny), so a restore or
/// another instance never leaves a stale copy in memory. Written to a temp file first, then moved over the old one:
/// a power cut never leaves half a file.
/// </summary>
public sealed class BackupStateStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _file;
    private readonly Lock _lock = new();

    public BackupStateStore(BackupPaths paths)
    {
        _file = paths.StateFile;
    }

    public BackupState Read()
    {
        lock (_lock)
        {
            return ReadFile();
        }
    }

    public BackupState Update(Action<BackupState> change)
    {
        lock (_lock)
        {
            var state = ReadFile();
            change(state);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_file))!);
            var temp = _file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state, Json));
            File.Move(temp, _file, overwrite: true);
            return state;
        }
    }

    private BackupState ReadFile()
    {
        if (!File.Exists(_file))
        {
            return new BackupState();
        }

        try
        {
            return JsonSerializer.Deserialize<BackupState>(File.ReadAllText(_file), Json) ?? new BackupState();
        }
        catch (JsonException)
        {
            // A damaged state file must never stop the app: start over (the backups themselves are untouched).
            return new BackupState();
        }
    }
}
