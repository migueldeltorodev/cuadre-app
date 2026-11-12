using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.CashBoxes;

[Collection(nameof(ApiCollection))]
public class GetCashBoxEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Get_ShouldReturnCashBox_WhenItExists()
    {
        // Arrange
        var bank = await Client.GetCashBoxByNameAsync("Banco CUP", Ct);

        // Act
        var response = await Client.GetAsync(Route(ApiEndpoints.CashBoxes.Get, bank.Id), Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CashBoxResponse>(Ct);
        body!.Id.Should().Be(bank.Id);
        body.Kind.Should().Be(CashBoxType.Bank);
    }

    [Fact]
    public async Task Get_ShouldReturnNotFound_WhenCashBoxDoesNotExist()
    {
        // Act
        var response = await Client.GetAsync(Route(ApiEndpoints.CashBoxes.Get, Guid.NewGuid()), Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
