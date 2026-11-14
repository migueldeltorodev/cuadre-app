using System.Text.Json.Serialization;

namespace Cuadre.Contracts.Enums;

/// <summary>RF-REP-08: why merchandise was lost. House consumption and adjustments are not losses.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LossType>))]
public enum LossType
{
    Shrinkage,
    Expired,
    Damaged,

    /// <summary>A count found less than the stock said.</summary>
    CountDifference
}
