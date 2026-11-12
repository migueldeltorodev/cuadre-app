using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Fakers;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Settings;

[Collection(nameof(ApiCollection))]
public class UpdateBusinessSettingsEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Update_ShouldPersistChanges_WhenRequestIsValid()
    {
        // Arrange — a repeated day must be stored once
        var request = BusinessSettingsFaker.UpdateRequest(closedDays: [Weekday.Sunday, Weekday.Sunday]);

        // Act
        var response = await Client.PutAsJsonAsync(ApiEndpoints.Settings.Update, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Client.GetFromJsonAsync<BusinessSettingsResponse>(ApiEndpoints.Settings.Get, Ct);
        stored!.Name.Should().Be("Bodegón La Esquina");
        stored.ClosedDays.Should().Equal(Weekday.Sunday);
        stored.ExchangeBufferCents.Should().Be(2_000);
        stored.DefaultMarkupBps.Should().Be(3_000);
        stored.PriceRoundingStepCents.Should().Be(500);
        stored.UpdatedAt.Should().Be(Clock.Now.UtcDateTime);
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequest_WhenNameIsBlank()
    {
        // Arrange — blank becomes empty after trimming
        var request = BusinessSettingsFaker.UpdateRequest(name: "   ");

        // Act
        var response = await Client.PutAsJsonAsync(ApiEndpoints.Settings.Update, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ValidationFailureResponse>(Ct);
        body!.Errors.Should().ContainSingle(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequest_WhenEveryDayIsClosed()
    {
        // Arrange
        var request = BusinessSettingsFaker.UpdateRequest(closedDays: Enum.GetValues<Weekday>());

        // Act
        var response = await Client.PutAsJsonAsync(ApiEndpoints.Settings.Update, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ValidationFailureResponse>(Ct);
        body!.Errors.Should().ContainSingle(e => e.PropertyName == "ClosedDays");
    }
}
