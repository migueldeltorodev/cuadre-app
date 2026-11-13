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

/// <summary>Pending = a shortage to claim; Recorded = a surplus, kept in the history; Voided = its day was reopened.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CashDiscrepancyState>))]
public enum CashDiscrepancyState
{
    Pending,
    Recorded,
    Voided
}
