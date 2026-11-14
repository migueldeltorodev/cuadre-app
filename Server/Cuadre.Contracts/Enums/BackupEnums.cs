using System.Text.Json.Serialization;

namespace Cuadre.Contracts.Enums;

/// <summary>Why a backup was made.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BackupReasonCode>))]
public enum BackupReasonCode
{
    Closing,
    AppExit,
    Manual,
    BeforeUpdate,
    BeforeRestore
}

/// <summary>The PC's own folder or the pendrive (RF-DAT-02).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BackupLocationCode>))]
public enum BackupLocationCode
{
    Local,
    External
}

/// <summary>RF-DAT-06, the most urgent first.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BackupAlertCode>))]
public enum BackupAlertCode
{
    Failed,
    NotConfigured,
    NeverCopied,
    Overdue
}

/// <summary>What happened with the pendrive copy. Unavailable = not plugged in (not a failure).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExternalCopyCode>))]
public enum ExternalCopyCode
{
    Copied,
    NotConfigured,
    Unavailable,
    Failed
}
