using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Requests;

public class UpdateBackupSettingsRequest
{
    /// <summary>The pendrive folder, full path (E:\Cuadre respaldos). Null/blank = none. Created when missing.</summary>
    public string? ExternalDirectory { get; init; }

    /// <summary>Warn when the last pendrive copy is older than this (1-60).</summary>
    public required int AlertAfterDays { get; init; }
}

public class RestoreBackupRequest
{
    public required BackupLocationCode Location { get; init; }
    public required string FileName { get; init; }
}
