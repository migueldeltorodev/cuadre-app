using System.Text.Json.Serialization;

namespace Cuadre.Contracts.Enums;

/// <summary>RF-DAT-08: the kinds of entries in the audit log.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditEventKindCode>))]
public enum AuditEventKindCode
{
    Voided,
    DayReopened,
    PriceChanged,
    ProfitFirstPlan,
    PayScheme,
    ConfigChanged
}

/// <summary>What an entry is about: the screen the client links to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditSubjectCode>))]
public enum AuditSubjectCode
{
    Purchase,
    StockOperation,
    EnvelopeOperation,
    Expense,
    ShortageRecovery,
    DailyClosing,
    Product,
    AllocationPlan,
    StaffPayScheme,
    Settings,
    CashBox,
    PaymentMethod,
    ExchangeRate,
    Envelope,
    Employee,
    ExpenseCategory,
    Tax
}
