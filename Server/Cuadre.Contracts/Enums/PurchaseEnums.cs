using System.Text.Json.Serialization;

namespace Cuadre.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<PurchaseState>))]
public enum PurchaseState
{
    Confirmed,
    Voided
}

/// <summary>Almacén = Storage, Mostrador = Counter.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LocationType>))]
public enum LocationType
{
    Storage,
    Counter
}
