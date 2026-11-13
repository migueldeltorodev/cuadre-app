using System.Text.Json.Serialization;

namespace Cuadre.Contracts.Enums;

/// <summary>What the app's rules use an envelope for. Custom = created by the user.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EnvelopeType>))]
public enum EnvelopeType
{
    Profit,
    OwnerPay,
    Tax,
    Operations,
    Replenishment,
    Custom
}

/// <summary>Saldo inicial, movimiento entre sobres, retiro, pago de impuestos.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EnvelopeOperationType>))]
public enum EnvelopeOperationType
{
    Opening,
    Transfer,
    Withdrawal,
    TaxPayment
}

[JsonConverter(typeof(JsonStringEnumConverter<EnvelopeOperationState>))]
public enum EnvelopeOperationState
{
    Confirmed,
    Voided
}

/// <summary>What wrote a row of the cash ledger.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CashMovementType>))]
public enum CashMovementType
{
    Purchase,
    PurchaseCharge,
    PurchaseVoid,
    Opening,
    EnvelopeTransfer,
    Withdrawal,
    TaxPayment,
    HouseConsumption,
    EnvelopeOperationVoid,
    StockOperationVoid,
    Collection,
    Allocation,
    StaffPay,
    ClosingVoid,
    ShortageRecovery,
    ShortageRecoveryVoid
}
