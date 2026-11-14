using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

public class BackupEntryResponse
{
    public required string FileName { get; init; }
    public required BackupLocationCode Location { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required BackupReasonCode Reason { get; init; }
    public required long SizeBytes { get; init; }
}

public class BackupMarkResponse
{
    public required DateTime At { get; init; }
    public required string FileName { get; init; }
}

public class BackupProblemResponse
{
    public required DateTime At { get; init; }
    public required string Message { get; init; }
}

public class BackupAlertResponse
{
    public required BackupAlertCode Kind { get; init; }

    /// <summary>Days since the last pendrive copy; null when there never was one.</summary>
    public int? DaysSinceExternal { get; init; }
}

/// <summary>The backup screen and the home alert (RF-DAT-06).</summary>
public class BackupStatusResponse
{
    public string? ExternalDirectory { get; init; }
    public required int AlertAfterDays { get; init; }

    /// <summary>The folder is there right now (the pendrive is plugged in).</summary>
    public required bool ExternalAvailable { get; init; }

    public BackupMarkResponse? LastLocal { get; init; }
    public BackupMarkResponse? LastExternal { get; init; }
    public BackupProblemResponse? LastProblem { get; init; }

    /// <summary>Null = all good.</summary>
    public BackupAlertResponse? Alert { get; init; }

    /// <summary>Both folders, the newest first. The pendrive's only when it is plugged in.</summary>
    public required IReadOnlyList<BackupEntryResponse> Backups { get; init; } = [];
}

public class BackupResultResponse
{
    public required BackupEntryResponse Backup { get; init; }
    public required ExternalCopyCode External { get; init; }

    /// <summary>Why the pendrive copy was not made, in words for the owner.</summary>
    public string? ExternalMessage { get; init; }
}

public class RestoreResultResponse
{
    public required BackupEntryResponse Restored { get; init; }

    /// <summary>When the restored copy was made: the data is back to this moment.</summary>
    public required DateTime RestoredCreatedAt { get; init; }

    /// <summary>The copy of what was there before the restore, in case of regrets.</summary>
    public required BackupEntryResponse SafetyCopy { get; init; }
}

public class RemovableDriveResponse
{
    public required string RootPath { get; init; }
    public required string Label { get; init; }
    public required long TotalBytes { get; init; }
    public required long FreeBytes { get; init; }
}

public class RemovableDrivesResponse
{
    public required IReadOnlyList<RemovableDriveResponse> Items { get; init; } = [];
}
