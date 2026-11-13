using System.Text.Json.Serialization;

namespace Cuadre.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<ExpenseState>))]
public enum ExpenseState
{
    Confirmed,
    Voided
}

/// <summary>Monthly: DueDay = day of the month (1-28). Weekly: DueDay = <see cref="Weekday"/> as a number (0 = Sunday).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExpenseFrequency>))]
public enum ExpenseFrequency
{
    Monthly,
    Weekly
}
