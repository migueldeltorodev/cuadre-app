using Cuadre.Application.Models;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>
/// The ONLY place where Contracts (wire DTOs) and Application models meet.
/// Plain extension methods: explicit, debuggable, no reflection-based mapper.
/// Timestamps are NOT set here: the services stamp them with the app clock (TimeProvider).
/// </summary>
public static class ContractMapping
{
    // ---------- Business settings ----------

    public static BusinessSettings MapToBusinessSettings(this UpdateBusinessSettingsRequest request)
    {
        return new BusinessSettings
        {
            Name = request.Name.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            ClosedDays = request.ClosedDays.Select(day => (DayOfWeek)(int)day).ToArray(),
            CashDifferenceThresholdCents = request.CashDifferenceThresholdCents,
            ExchangeBufferCents = request.ExchangeBufferCents,
            DefaultMarkupBps = request.DefaultMarkupBps,
            PriceRoundingStepCents = request.PriceRoundingStepCents
        };
    }

    public static BusinessSettingsResponse MapToResponse(this BusinessSettings settings)
    {
        return new BusinessSettingsResponse
        {
            Name = settings.Name,
            Address = settings.Address,
            ClosedDays = settings.ClosedDays.Select(day => (Weekday)(int)day).ToArray(),
            CashDifferenceThresholdCents = settings.CashDifferenceThresholdCents,
            ExchangeBufferCents = settings.ExchangeBufferCents,
            DefaultMarkupBps = settings.DefaultMarkupBps,
            PriceRoundingStepCents = settings.PriceRoundingStepCents,
            UpdatedAt = settings.UpdatedAt
        };
    }

    // ---------- Cash boxes ----------

    public static CashBox MapToCashBox(this CreateCashBoxRequest request)
    {
        return new CashBox
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Currency = request.Currency.ToModel(),
            Kind = request.Kind.ToModel(),
            IsActive = true
        };
    }

    public static CashBoxChanges MapToCashBoxChanges(this UpdateCashBoxRequest request, Guid id)
    {
        return new CashBoxChanges(id, request.Name.Trim(), request.IsActive);
    }

    public static CashBoxResponse MapToResponse(this CashBox cashBox)
    {
        return new CashBoxResponse
        {
            Id = cashBox.Id,
            Name = cashBox.Name,
            Currency = cashBox.Currency.ToContract(),
            Kind = cashBox.Kind.ToContract(),
            IsActive = cashBox.IsActive,
            CreatedAt = cashBox.CreatedAt,
            UpdatedAt = cashBox.UpdatedAt
        };
    }

    public static CashBoxesResponse MapToResponse(this IEnumerable<CashBox> cashBoxes)
    {
        return new CashBoxesResponse { Items = cashBoxes.Select(MapToResponse) };
    }

    // ---------- Payment methods ----------

    public static PaymentMethod MapToPaymentMethod(this CreatePaymentMethodRequest request)
    {
        return new PaymentMethod
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CashBoxId = request.CashBoxId,
            IsActive = true
        };
    }

    public static PaymentMethod MapToPaymentMethod(this UpdatePaymentMethodRequest request, Guid id)
    {
        return new PaymentMethod
        {
            Id = id,
            Name = request.Name.Trim(),
            CashBoxId = request.CashBoxId,
            IsActive = request.IsActive
        };
    }

    public static PaymentMethodResponse MapToResponse(this PaymentMethod paymentMethod)
    {
        return new PaymentMethodResponse
        {
            Id = paymentMethod.Id,
            Name = paymentMethod.Name,
            CashBoxId = paymentMethod.CashBoxId,
            IsActive = paymentMethod.IsActive,
            CreatedAt = paymentMethod.CreatedAt,
            UpdatedAt = paymentMethod.UpdatedAt
        };
    }

    public static PaymentMethodsResponse MapToResponse(this IEnumerable<PaymentMethod> paymentMethods)
    {
        return new PaymentMethodsResponse { Items = paymentMethods.Select(MapToResponse) };
    }

    // ---------- Exchange rates ----------

    public static ExchangeRateResponse MapToResponse(this ExchangeRate rate)
    {
        return new ExchangeRateResponse
        {
            Date = rate.Date,
            CupPerUsdCents = rate.CupPerUsdCents,
            UpdatedAt = rate.UpdatedAt
        };
    }

    public static ExchangeRatesResponse MapToResponse(this IEnumerable<ExchangeRate> rates,
        int page, int pageSize, int total)
    {
        return new ExchangeRatesResponse
        {
            Items = rates.Select(MapToResponse),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public static CurrentExchangeRateResponse MapToResponse(this CurrentExchangeRate current)
    {
        return new CurrentExchangeRateResponse
        {
            Today = current.Today,
            IsMissing = current.IsMissing,
            Latest = current.Latest?.MapToResponse()
        };
    }

    public static GetAllExchangeRatesOptions MapToOptions(this GetAllExchangeRatesRequest request)
    {
        return new GetAllExchangeRatesOptions
        {
            From = request.From,
            To = request.To,
            Page = request.Page.GetValueOrDefault(PagedRequest.DefaultPage),
            PageSize = request.PageSize.GetValueOrDefault(PagedRequest.DefaultPageSize)
        };
    }

    // ---------- Enums ----------
    // Inbound: an undefined value (a client can send "currency": 5) is passed through as-is so the
    // validator's IsInEnum rejects it with a 400, instead of a 500 here.
    // Outbound: a model value with no wire equivalent is a bug, so it fails loudly.

    private static Currency ToModel(this CurrencyCode code) => code switch
    {
        CurrencyCode.CUP => Currency.CUP,
        CurrencyCode.USD => Currency.USD,
        _ => (Currency)(int)code
    };

    private static CurrencyCode ToContract(this Currency currency) => currency switch
    {
        Currency.CUP => CurrencyCode.CUP,
        Currency.USD => CurrencyCode.USD,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null)
    };

    private static CashBoxKind ToModel(this CashBoxType type) => type switch
    {
        CashBoxType.Cash => CashBoxKind.Cash,
        CashBoxType.Bank => CashBoxKind.Bank,
        _ => (CashBoxKind)(int)type
    };

    private static CashBoxType ToContract(this CashBoxKind kind) => kind switch
    {
        CashBoxKind.Cash => CashBoxType.Cash,
        CashBoxKind.Bank => CashBoxType.Bank,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
