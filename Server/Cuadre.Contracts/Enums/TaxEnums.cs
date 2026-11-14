using System.Text.Json.Serialization;

namespace Cuadre.Contracts.Enums;

/// <summary>D-62: what a tax is calculated over.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TaxBaseCode>))]
public enum TaxBaseCode
{
    /// <summary>What was collected (A1).</summary>
    Sales,

    /// <summary>The profit before taxes (0 when there was a loss).</summary>
    Profit
}
