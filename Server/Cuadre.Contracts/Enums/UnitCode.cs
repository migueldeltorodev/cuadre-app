using System.Text.Json.Serialization;

namespace Cuadre.Contracts.Enums;

/// <summary>Measure units on the wire. The Spanish labels live in the UI.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UnitCode>))]
public enum UnitCode
{
    Each,
    Pound,
    Kilogram,
    Liter,
    Sack,
    Box,
    Pack,
    Dozen,
    Bag
}

[JsonConverter(typeof(JsonStringEnumConverter<PriceChangeKind>))]
public enum PriceChangeKind
{
    Manual,
    Purchase
}
