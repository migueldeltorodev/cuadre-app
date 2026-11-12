using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.ExchangeRates;

[Collection(nameof(ApiCollection))]
public class GetAllExchangeRatesEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAll_ShouldReturnNewestFirst_WithinTheRange()
    {
        // Arrange — five consecutive days, rates 40,000..40,004
        for (var i = 0; i < 5; i++)
        {
            await Client.PutAsJsonAsync(Route(ApiEndpoints.ExchangeRates.Upsert, Clock.Today.AddDays(-i)),
                new UpsertExchangeRateRequest { CupPerUsdCents = 40_000 + i }, Ct);
        }

        var from = Clock.Today.AddDays(-3).ToString("yyyy-MM-dd");
        var to = Clock.Today.AddDays(-1).ToString("yyyy-MM-dd");

        // Act
        var response = await Client.GetAsync($"{ApiEndpoints.ExchangeRates.GetAll}?from={from}&to={to}", Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ExchangeRatesResponse>(Ct);
        page!.Total.Should().Be(3);
        page.Items.Select(x => x.Date).Should().Equal(
            Clock.Today.AddDays(-1), Clock.Today.AddDays(-2), Clock.Today.AddDays(-3));
    }

    [Fact]
    public async Task GetAll_ShouldPage_WhenPageSizeIsSmall()
    {
        // Arrange
        for (var i = 0; i < 3; i++)
        {
            await Client.PutAsJsonAsync(Route(ApiEndpoints.ExchangeRates.Upsert, Clock.Today.AddDays(-i)),
                new UpsertExchangeRateRequest { CupPerUsdCents = 40_000 }, Ct);
        }

        // Act
        var page = await Client.GetFromJsonAsync<ExchangeRatesResponse>(
            $"{ApiEndpoints.ExchangeRates.GetAll}?page=2&pageSize=2", Ct);

        // Assert
        page!.Total.Should().Be(3);
        page.Items.Should().ContainSingle().Which.Date.Should().Be(Clock.Today.AddDays(-2));
        page.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_ShouldReturnBadRequest_WhenFromIsAfterTo()
    {
        // Act
        var response = await Client.GetAsync(
            $"{ApiEndpoints.ExchangeRates.GetAll}?from=2026-11-10&to=2026-11-01", Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
