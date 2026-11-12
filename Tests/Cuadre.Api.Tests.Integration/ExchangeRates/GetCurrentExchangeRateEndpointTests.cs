using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.ExchangeRates;

[Collection(nameof(ApiCollection))]
public class GetCurrentExchangeRateEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetCurrent_ShouldBeMissingWithNoLatest_WhenNoRateWasEverLoaded()
    {
        // Act
        var response = await Client.GetAsync(ApiEndpoints.ExchangeRates.Current, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var current = await response.Content.ReadFromJsonAsync<CurrentExchangeRateResponse>(Ct);
        current!.Today.Should().Be(Clock.Today);
        current.IsMissing.Should().BeTrue();
        current.Latest.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrent_ShouldBeMissingButShowYesterday_WhenTodayWasNotLoaded()
    {
        // Arrange
        var yesterday = Clock.Today.AddDays(-1);
        await Client.PutAsJsonAsync(Route(ApiEndpoints.ExchangeRates.Upsert, yesterday),
            new UpsertExchangeRateRequest { CupPerUsdCents = 40_000 }, Ct);

        // Act
        var current = await Client.GetFromJsonAsync<CurrentExchangeRateResponse>(ApiEndpoints.ExchangeRates.Current, Ct);

        // Assert — the warning shows, but the UI can still offer yesterday's rate as reference
        current!.IsMissing.Should().BeTrue();
        current.Latest!.Date.Should().Be(yesterday);
        current.Latest.CupPerUsdCents.Should().Be(40_000);
    }

    [Fact]
    public async Task GetCurrent_ShouldNotBeMissing_WhenTodayWasLoaded()
    {
        // Arrange
        await Client.PutAsJsonAsync(Route(ApiEndpoints.ExchangeRates.Upsert, Clock.Today),
            new UpsertExchangeRateRequest { CupPerUsdCents = 41_000 }, Ct);

        // Act
        var current = await Client.GetFromJsonAsync<CurrentExchangeRateResponse>(ApiEndpoints.ExchangeRates.Current, Ct);

        // Assert
        current!.IsMissing.Should().BeFalse();
        current.Latest!.Date.Should().Be(Clock.Today);
    }

    [Fact]
    public async Task GetCurrent_ShouldBeMissingAgain_WhenTheDayChanges()
    {
        // Arrange — loaded today, then the clock passes midnight
        await Client.PutAsJsonAsync(Route(ApiEndpoints.ExchangeRates.Upsert, Clock.Today),
            new UpsertExchangeRateRequest { CupPerUsdCents = 41_000 }, Ct);
        Clock.Now = Clock.Now.AddDays(1);

        // Act
        var current = await Client.GetFromJsonAsync<CurrentExchangeRateResponse>(ApiEndpoints.ExchangeRates.Current, Ct);

        // Assert
        current!.IsMissing.Should().BeTrue();
        current.Today.Should().Be(Clock.Today);
    }
}
