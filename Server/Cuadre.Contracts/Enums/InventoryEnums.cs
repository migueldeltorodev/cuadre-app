using System.Text.Json.Serialization;

namespace Cuadre.Contracts.Enums;

/// <summary>Traslado, salida, conteo.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StockOperationType>))]
public enum StockOperationType
{
    Transfer,
    Exit,
    Count
}

[JsonConverter(typeof(JsonStringEnumConverter<StockOperationState>))]
public enum StockOperationState
{
    Confirmed,
    Voided
}

/// <summary>Merma, vencido, roto, consumo de la casa, ajuste (RF-INV-04).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExitReasonCode>))]
public enum ExitReasonCode
{
    Shrinkage,
    Expired,
    Damaged,
    HouseConsumption,
    Adjustment
}

/// <summary>What wrote a row of the stock ledger.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StockMovementType>))]
public enum StockMovementType
{
    Purchase,
    PurchaseVoid,
    TransferOut,
    TransferIn,
    Exit,
    CountAdjustment,
    OperationVoid,
    Sale,
    ClosingVoid
}
