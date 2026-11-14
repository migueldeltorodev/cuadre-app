using System.Globalization;
using System.Text.RegularExpressions;

namespace Cuadre.Application.Models;

// Backups (RF-DAT-01..03, 05, 06). A backup is a complete, verified copy of the database at one moment,
// zipped with a manifest. Pure rules here (names, rotation, alert); files and SQLite live in BackupService.

public enum BackupReason
{
    /// <summary>Right after a daily closing is confirmed (RF-DAT-01).</summary>
    Closing,

    /// <summary>When the app window is closed (RF-DAT-01).</summary>
    AppExit,

    /// <summary>"Respaldar ahora".</summary>
    Manual,

    /// <summary>Before a new version upgrades the database (D-57). A safety copy.</summary>
    BeforeUpdate,

    /// <summary>Before a restore replaces the data (RF-DAT-05). A safety copy.</summary>
    BeforeRestore
}

public enum BackupLocation
{
    /// <summary>The PC's own folder (%LOCALAPPDATA%\Cuadre\backups).</summary>
    Local,

    /// <summary>The folder the owner chose: the pendrive (RF-DAT-02).</summary>
    External
}

public static class BackupReasons
{
    /// <summary>Safety copies are rotated apart: a restore or an update must never push out the daily ones.</summary>
    public static bool IsSafetyCopy(this BackupReason reason) =>
        reason is BackupReason.BeforeUpdate or BackupReason.BeforeRestore;

    /// <summary>The word in the file name: the owner may look at the pendrive with the Explorer.</summary>
    public static string Slug(this BackupReason reason) => reason switch
    {
        BackupReason.Closing => "cierre",
        BackupReason.AppExit => "al-salir",
        BackupReason.Manual => "manual",
        BackupReason.BeforeUpdate => "antes-de-actualizar",
        BackupReason.BeforeRestore => "antes-de-restaurar",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };

    public static BackupReason? FromSlug(string slug) =>
        Enum.GetValues<BackupReason>().Select(r => (BackupReason?)r).FirstOrDefault(r => r!.Value.Slug() == slug);
}

/// <summary>One backup file found in a folder. Everything comes from its name: listing never opens a zip.</summary>
public sealed record BackupEntry(
    string FileName,
    BackupLocation Location,
    DateTime CreatedAtLocal,
    DateTime CreatedAtUtc,
    BackupReason Reason,
    long SizeBytes);

/// <summary>
/// cuadre-2026-11-14_213005-cierre.zip (local time: it is what the owner reads on the pendrive).
/// Two backups in the same second get "-2", "-3"... The strict pattern also keeps a restore from ever
/// reading a path outside the backup folders.
/// </summary>
public static partial class BackupNaming
{
    public const string Extension = ".zip";
    public const string SearchPattern = "cuadre-*.zip";

    [GeneratedRegex(@"^cuadre-(?<date>\d{4}-\d{2}-\d{2})_(?<time>\d{6})-(?<reason>[a-z]+(?:-[a-z]+)*?)(?:-(?<n>\d+))?\.zip$")]
    private static partial Regex Pattern();

    public static string Format(DateTime local, BackupReason reason, int sequence = 1)
    {
        var suffix = sequence > 1 ? $"-{sequence}" : "";
        return $"cuadre-{local:yyyy-MM-dd}_{local:HHmmss}-{reason.Slug()}{suffix}{Extension}";
    }

    public static bool TryParse(string fileName, out DateTime createdAtLocal, out BackupReason reason)
    {
        createdAtLocal = default;
        reason = default;

        var match = Pattern().Match(fileName);
        if (!match.Success
            || BackupReasons.FromSlug(match.Groups["reason"].Value) is not { } parsed
            || !DateTime.TryParseExact($"{match.Groups["date"].Value} {match.Groups["time"].Value}", "yyyy-MM-dd HHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return false;
        }

        createdAtLocal = local;
        reason = parsed;
        return true;
    }

    /// <summary>
    /// 1 for "...-manual.zip", 2 for "...-manual-2.zip". Ties in the same second are broken by THIS number:
    /// as text, "manual.zip" sorts after "manual-2.zip" ('.' &gt; '-') and the newest would look the oldest.
    /// </summary>
    public static int SequenceOf(string fileName)
    {
        var match = Pattern().Match(fileName);
        return match.Success && int.TryParse(match.Groups["n"].Value, out var n) ? n : 1;
    }
}

/// <summary>
/// RF-DAT-03: in each folder keep the latest backup of each of the last 7 days that have one, of the last 4 weeks
/// and of the last 12 months ("days that have one": a shop closed for two weeks still keeps 7 daily backups).
/// Safety copies (before an update or a restore) are kept apart: the last 5. The newest backup is never deleted.
/// </summary>
public static class BackupRotation
{
    public const int Daily = 7;
    public const int Weekly = 4;
    public const int Monthly = 12;
    public const int SafetyCopies = 5;

    public static IReadOnlyList<BackupEntry> SelectToDelete(IEnumerable<BackupEntry> entries)
    {
        var all = entries
            .OrderByDescending(e => e.CreatedAtLocal)
            .ThenByDescending(e => BackupNaming.SequenceOf(e.FileName))
            .ThenByDescending(e => e.FileName, StringComparer.Ordinal)
            .ToList();
        var regular = all.Where(e => !e.Reason.IsSafetyCopy()).ToList();
        var keep = new HashSet<string>();

        KeepNewestOfEach(regular, e => DateOnly.FromDateTime(e.CreatedAtLocal), Daily, keep);
        KeepNewestOfEach(regular, e => (ISOWeek.GetYear(e.CreatedAtLocal), ISOWeek.GetWeekOfYear(e.CreatedAtLocal)),
            Weekly, keep);
        KeepNewestOfEach(regular, e => (e.CreatedAtLocal.Year, e.CreatedAtLocal.Month), Monthly, keep);

        foreach (var safety in all.Where(e => e.Reason.IsSafetyCopy()).Take(SafetyCopies))
        {
            keep.Add(safety.FileName);
        }

        return all.Where(e => !keep.Contains(e.FileName)).ToList();
    }

    /// <summary>Entries come newest first, so each group's first element is its newest and groups come newest first.</summary>
    private static void KeepNewestOfEach<TKey>(List<BackupEntry> newestFirst, Func<BackupEntry, TKey> period,
        int count, HashSet<string> keep)
    {
        foreach (var group in newestFirst.GroupBy(period).Take(count))
        {
            keep.Add(group.First().FileName);
        }
    }
}

/// <summary>What travels inside each zip next to the database.</summary>
public sealed class BackupManifest
{
    public const int CurrentFormat = 1;
    public const string EntryName = "manifest.json";
    public const string DatabaseEntryName = "cuadre.db";

    public int Format { get; init; } = CurrentFormat;
    public string App { get; init; } = "Cuadre";
    public DateTime CreatedAtUtc { get; init; }
    public BackupReason Reason { get; init; }

    /// <summary>The last migration the copy has: a newer app can open it, an older one must refuse it.</summary>
    public string? SchemaVersion { get; init; }

    /// <summary>SHA-256 of the database entry: a damaged copy is caught before it replaces anything.</summary>
    public string DatabaseSha256 { get; init; } = "";

    public long DatabaseBytes { get; init; }
}

/// <summary>What the owner chooses (RF-DAT-02, RF-DAT-06).</summary>
public sealed class BackupSettings
{
    public const int DefaultAlertAfterDays = 7;

    /// <summary>The pendrive folder (E:\Cuadre respaldos). Null = not chosen yet.</summary>
    public string? ExternalDirectory { get; set; }

    /// <summary>Warn on the home screen when the last external copy is older than this (proposal: under 7 days).</summary>
    public int AlertAfterDays { get; set; } = DefaultAlertAfterDays;
}

public sealed record BackupMark(DateTime AtUtc, string FileName);

public sealed record BackupProblem(DateTime AtUtc, string Message);

/// <summary>
/// D-56: kept in backup.json NEXT TO the database, never inside it. Restoring a 10-day-old copy must not make the
/// app forget that it backed up yesterday (nor where the pendrive folder is).
/// </summary>
public sealed class BackupState
{
    public BackupSettings Settings { get; set; } = new();
    public BackupMark? LastLocal { get; set; }
    public BackupMark? LastExternal { get; set; }

    /// <summary>The last REAL failure (copy, verification, disk). A pendrive not plugged in is not a failure.</summary>
    public BackupProblem? LastProblem { get; set; }
}

public enum BackupAlertKind
{
    /// <summary>The last backup failed: the most urgent.</summary>
    Failed,

    /// <summary>No pendrive folder chosen.</summary>
    NotConfigured,

    /// <summary>A folder is chosen but nothing was ever copied there.</summary>
    NeverCopied,

    /// <summary>The last external copy is older than the setting.</summary>
    Overdue
}

public sealed record BackupAlert(BackupAlertKind Kind, int? DaysSinceExternal);

public static class BackupAlerts
{
    /// <summary>RF-DAT-06. Null = all good.</summary>
    public static BackupAlert? Evaluate(BackupState state, DateOnly today, TimeZoneInfo timeZone)
    {
        if (state.LastProblem is not null)
        {
            return new BackupAlert(BackupAlertKind.Failed, DaysSince(state.LastExternal, today, timeZone));
        }

        if (string.IsNullOrWhiteSpace(state.Settings.ExternalDirectory))
        {
            return new BackupAlert(BackupAlertKind.NotConfigured, null);
        }

        if (state.LastExternal is null)
        {
            return new BackupAlert(BackupAlertKind.NeverCopied, null);
        }

        var days = DaysSince(state.LastExternal, today, timeZone)!.Value;
        return days > state.Settings.AlertAfterDays ? new BackupAlert(BackupAlertKind.Overdue, days) : null;
    }

    private static int? DaysSince(BackupMark? mark, DateOnly today, TimeZoneInfo timeZone) =>
        mark is null
            ? null
            : today.DayNumber - DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(mark.AtUtc, timeZone)).DayNumber;
}

public enum ExternalCopyOutcome
{
    Copied,
    NotConfigured,

    /// <summary>The folder is not there: the pendrive is not plugged in. Not a failure.</summary>
    Unavailable,
    Failed
}

public sealed record BackupResult(BackupEntry Backup, ExternalCopyOutcome External, string? ExternalMessage);

public sealed record RestoreResult(BackupEntry Restored, DateTime RestoredCreatedAtUtc, BackupEntry SafetyCopy);

public sealed record RemovableDrive(string RootPath, string Label, long TotalBytes, long FreeBytes);

/// <summary>Everything the backup screen and the home alert need.</summary>
public sealed record BackupStatus(
    BackupSettings Settings,
    bool ExternalAvailable,
    BackupMark? LastLocal,
    BackupMark? LastExternal,
    BackupProblem? LastProblem,
    BackupAlert? Alert,
    IReadOnlyList<BackupEntry> Backups);
