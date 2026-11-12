using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.ExchangeRates;

[Collection(nameof(ApiCollection))]
public class GetExchangeRateEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Get_ShouldReturnRate_WhenDateWasLoaded()
    {
        // Arrange
        await Client.PutAsJsonAsync(Route(ApiEndpoints.ExchangeRates.Upsert, Clock.Today),
            new UpsertExchangeRateRequest { CupPerUsdCents = 41_000 }, Ct);

        // Act
        var response = await Client.GetAsync(Route(ApiEndpoints.ExchangeRates.Get, Clock.Today), Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rate = await response.Content.ReadFromJsonAsync<ExchangeRateResponse>(Ct);
        rate!.CupPerUsdCents.Should().Be(41_000);
    }

    [Fact]
    public async Task Get_ShouldReturnNotFound_WhenDateHasNoRate()
    {
        // Act
        var response = await Client.GetAsync(Route(ApiEndpoints.ExchangeRates.Get, Clock.Today), Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
