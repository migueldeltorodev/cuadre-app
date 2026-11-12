using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Settings;

[Collection(nameof(ApiCollection))]
public class GetBusinessSettingsEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Get_ShouldReturnFactoryDefaults_WhenNeverChanged()
    {
        // Act
        var response = await Client.GetAsync(ApiEndpoints.Settings.Get, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<BusinessSettingsResponse>(Ct);
        settings!.Name.Should().Be("Mi negocio");
        settings.ClosedDays.Should().BeEmpty();
        settings.CashDifferenceThresholdCents.Should().Be(5_000);
        settings.PriceRoundingStepCents.Should().Be(100);
        settings.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
