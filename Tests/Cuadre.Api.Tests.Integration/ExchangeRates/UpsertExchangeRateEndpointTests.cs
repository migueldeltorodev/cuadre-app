using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.ExchangeRates;

[Collection(nameof(ApiCollection))]
public class UpsertExchangeRateEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Upsert_ShouldStoreRate_WhenDateIsToday()
    {
        // Arrange — 1 USD = 410.50 CUP
        var request = new UpsertExchangeRateRequest { CupPerUsdCents = 41_050 };

        // Act
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.ExchangeRates.Upsert, Clock.Today), request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rate = await response.Content.ReadFromJsonAsync<ExchangeRateResponse>(Ct);
        rate!.Date.Should().Be(Clock.Today);
        rate.CupPerUsdCents.Should().Be(41_050);
    }

    [Fact]
    public async Task Upsert_ShouldCorrectTheSameDay_InsteadOfAddingASecondRate()
    {
        // Arrange
        var route = Route(ApiEndpoints.ExchangeRates.Upsert, Clock.Today);
        await Client.PutAsJsonAsync(route, new UpsertExchangeRateRequest { CupPerUsdCents = 41_000 }, Ct);

        // Act — typo fixed later the same day
        var response = await Client.PutAsJsonAsync(route, new UpsertExchangeRateRequest { CupPerUsdCents = 42_000 }, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await Client.GetFromJsonAsync<ExchangeRatesResponse>(ApiEndpoints.ExchangeRates.GetAll, Ct);
        history!.Total.Should().Be(1);
        history.Items.Single().CupPerUsdCents.Should().Be(42_000);
    }

    [Fact]
    public async Task Upsert_ShouldReturnUnprocessable_WhenDateIsInTheFuture()
    {
        // Arrange
        var tomorrow = Clock.Today.AddDays(1);

        // Act
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.ExchangeRates.Upsert, tomorrow),
            new UpsertExchangeRateRequest { CupPerUsdCents = 41_000 }, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Upsert_ShouldReturnBadRequest_WhenRateIsZero()
    {
        // Act
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.ExchangeRates.Upsert, Clock.Today),
            new UpsertExchangeRateRequest { CupPerUsdCents = 0 }, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ValidationFailureResponse>(Ct);
        body!.Errors.Should().ContainSingle(e => e.PropertyName == "CupPerUsdCents");
    }
}
