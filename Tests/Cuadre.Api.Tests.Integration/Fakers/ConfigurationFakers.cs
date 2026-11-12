using Bogus;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;

namespace Cuadre.Api.Tests.Integration.Fakers;

/// <summary>Valid requests with unique names, so they never collide with the seeded defaults.</summary>
public static class CashBoxFaker
{
    public static Faker<CreateCashBoxRequest> CreateRequest() =>
        new Faker<CreateCashBoxRequest>()
            .CustomInstantiator(f => new CreateCashBoxRequest
            {
                Name = $"Caja {f.Random.AlphaNumeric(8)}",
                Currency = f.PickRandom<CurrencyCode>(),
                Kind = f.PickRandom<CashBoxType>()
            });
}

public static class PaymentMethodFaker
{
    public static CreatePaymentMethodRequest CreateRequest(Guid cashBoxId) => new()
    {
        Name = $"Cobro {new Faker().Random.AlphaNumeric(8)}",
        CashBoxId = cashBoxId
    };
}

public static class BusinessSettingsFaker
{
    /// <summary>Contracts are init-only classes (no `with`), so the fields tests vary are parameters.</summary>
    public static UpdateBusinessSettingsRequest UpdateRequest(
        string name = "Bodegón La Esquina",
        IReadOnlyCollection<Weekday>? closedDays = null) => new()
    {
        Name = name,
        Address = "Calle 10 #123",
        ClosedDays = closedDays ?? [Weekday.Sunday],
        CashDifferenceThresholdCents = 10_000,   // 100 CUP
        ExchangeBufferCents = 2_000,             // 20 CUP over the rate
        DefaultMarkupBps = 3_000,                // 30 %
        PriceRoundingStepCents = 500             // round to 5 CUP
    };
}
