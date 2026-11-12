using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Requests;

// Money in cents, percentages in basis points (30% = 3000). The UI converts for display.

public class UpdateBusinessSettingsRequest
{
    public required string Name { get; init; }
    public string? Address { get; init; }
    public required IReadOnlyCollection<Weekday> ClosedDays { get; init; }
    public required long CashDifferenceThresholdCents { get; init; }
    public required long ExchangeBufferCents { get; init; }
    public required int DefaultMarkupBps { get; init; }
    public required long PriceRoundingStepCents { get; init; }
}

public class CreateCashBoxRequest
{
    public required string Name { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required CashBoxType Kind { get; init; }
}

/// <summary>Currency and kind are not here on purpose: they cannot change after creation.</summary>
public class UpdateCashBoxRequest
{
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}

public class CreatePaymentMethodRequest
{
    public required string Name { get; init; }
    public required Guid CashBoxId { get; init; }
}

public class UpdatePaymentMethodRequest
{
    public required string Name { get; init; }
    public required Guid CashBoxId { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>PUT /api/exchange-rates/{date}: the date is in the URL, one rate per day.</summary>
public class UpsertExchangeRateRequest
{
    /// <summary>1 USD = 410.50 CUP is sent as 41050.</summary>
    public required long CupPerUsdCents { get; init; }
}

public class GetAllExchangeRatesRequest : PagedRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}
