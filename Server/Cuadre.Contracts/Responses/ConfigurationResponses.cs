using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Responses;

public class BusinessSettingsResponse
{
    public required string Name { get; init; }
    public string? Address { get; init; }
    public required IReadOnlyCollection<Weekday> ClosedDays { get; init; }
    public required long CashDifferenceThresholdCents { get; init; }
    public required long ExchangeBufferCents { get; init; }
    public required int DefaultMarkupBps { get; init; }
    public required long PriceRoundingStepCents { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class CashBoxResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required CurrencyCode Currency { get; init; }
    public required CashBoxType Kind { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>Short configuration list: no paging needed.</summary>
public class CashBoxesResponse
{
    public required IEnumerable<CashBoxResponse> Items { get; init; } = [];
}

public class PaymentMethodResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid CashBoxId { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class PaymentMethodsResponse
{
    public required IEnumerable<PaymentMethodResponse> Items { get; init; } = [];
}

public class ExchangeRateResponse
{
    public required DateOnly Date { get; init; }
    public required long CupPerUsdCents { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class ExchangeRatesResponse : PagedResponse<ExchangeRateResponse>;

/// <summary>"Tasa de hoy" for the side menu. IsMissing = no rate typed for today yet (RF-MON-03).</summary>
public class CurrentExchangeRateResponse
{
    public required DateOnly Today { get; init; }
    public required bool IsMissing { get; init; }

    /// <summary>Latest rate on or before today (today's if loaded); null if none was ever loaded.</summary>
    public ExchangeRateResponse? Latest { get; init; }
}
