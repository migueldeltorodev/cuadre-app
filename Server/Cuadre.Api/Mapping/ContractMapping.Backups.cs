using Cuadre.Application.Models;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>Backups (RF-DAT-01..06). Every moment travels in UTC, like the rest of the API.</summary>
public static partial class ContractMapping
{
    public static BackupStatusResponse MapToResponse(this BackupStatus status) => new()
    {
        ExternalDirectory = status.Settings.ExternalDirectory,
        AlertAfterDays = status.Settings.AlertAfterDays,
        ExternalAvailable = status.ExternalAvailable,
        LastLocal = status.LastLocal?.MapToResponse(),
        LastExternal = status.LastExternal?.MapToResponse(),
        LastProblem = status.LastProblem is { } problem
            ? new BackupProblemResponse { At = Utc(problem.AtUtc), Message = problem.Message }
            : null,
        Alert = status.Alert is { } alert
            ? new BackupAlertResponse { Kind = (BackupAlertCode)alert.Kind, DaysSinceExternal = alert.DaysSinceExternal }
            : null,
        Backups = status.Backups.Select(entry => entry.MapToResponse()).ToList()
    };

    public static BackupResultResponse MapToResponse(this BackupResult result) => new()
    {
        Backup = result.Backup.MapToResponse(),
        External = (ExternalCopyCode)result.External,
        ExternalMessage = result.ExternalMessage
    };

    public static RestoreResultResponse MapToResponse(this RestoreResult result) => new()
    {
        Restored = result.Restored.MapToResponse(),
        RestoredCreatedAt = Utc(result.RestoredCreatedAtUtc),
        SafetyCopy = result.SafetyCopy.MapToResponse()
    };

    public static RemovableDrivesResponse MapToResponse(this IReadOnlyList<RemovableDrive> drives) => new()
    {
        Items = drives.Select(drive => new RemovableDriveResponse
        {
            RootPath = drive.RootPath,
            Label = drive.Label,
            TotalBytes = drive.TotalBytes,
            FreeBytes = drive.FreeBytes
        }).ToList()
    };

    public static BackupSettings MapToBackupSettings(this UpdateBackupSettingsRequest request) => new()
    {
        ExternalDirectory = string.IsNullOrWhiteSpace(request.ExternalDirectory) ? null : request.ExternalDirectory.Trim(),
        AlertAfterDays = request.AlertAfterDays
    };

    public static BackupLocation MapToBackupLocation(this BackupLocationCode code) => (BackupLocation)code;

    private static BackupEntryResponse MapToResponse(this BackupEntry entry) => new()
    {
        FileName = entry.FileName,
        Location = (BackupLocationCode)entry.Location,
        CreatedAt = Utc(entry.CreatedAtUtc),
        Reason = (BackupReasonCode)entry.Reason,
        SizeBytes = entry.SizeBytes
    };

    private static BackupMarkResponse MapToResponse(this BackupMark mark) => new()
    {
        At = Utc(mark.AtUtc),
        FileName = mark.FileName
    };

    /// <summary>backup.json round-trips keep the kind, but a "Z" in the JSON must never depend on it.</summary>
    private static DateTime Utc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
