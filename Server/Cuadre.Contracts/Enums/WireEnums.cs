using System.Text.Json.Serialization;

namespace Cuadre.Contracts.Enums;

// Wire enums, separate from the Application ones so the HTTP contract never changes by accident.
// The attribute makes ANY System.Text.Json client (the API, the tests, a future SDK) use names, not numbers.

[JsonConverter(typeof(JsonStringEnumConverter<CurrencyCode>))]
public enum CurrencyCode
{
    CUP,
    USD
}

[JsonConverter(typeof(JsonStringEnumConverter<CashBoxType>))]
public enum CashBoxType
{
    Cash,
    Bank
}

[JsonConverter(typeof(JsonStringEnumConverter<Weekday>))]
public enum Weekday
{
    Sunday,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday
}
