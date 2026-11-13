using System.Text.Json.Serialization;

namespace Cuadre.Contracts.Enums;

/// <summary>Voided = reopened (RF-CIE-10).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DailyClosingState>))]
public enum DailyClosingState
{
    Confirmed,
    Voided
}

/// <summary>Faltante / sobrante of the cash count.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CashDiscrepancyType>))]
public enum CashDiscrepancyType
{
    Shortage,
    Surplus
}

/// <summary>
/// Pending = a shortage to claim (maybe partly recovered); Recorded = a surplus, kept in the history; Voided = its day
/// was reopened; Recovered = a shortage fully recovered; Lost = a shortage written off.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CashDiscrepancyState>))]
public enum CashDiscrepancyState
{
    Pending,
    Recorded,
    Voided,
    Recovered,
    Lost
}

[JsonConverter(typeof(JsonStringEnumConverter<ShortageRecoveryState>))]
public enum ShortageRecoveryState
{
    Confirmed,
    Voided
}
