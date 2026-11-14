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

/// <summary>D-61: why a price needs a look.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PriceNoticeKindCode>))]
public enum PriceNoticeKindCode
{
    /// <summary>A new batch went on sale and its suggested price is not the price.</summary>
    BatchChanged,

    /// <summary>The next batch (bought, still waiting) will not fit in today's price.</summary>
    NewBatchCostlier
}
