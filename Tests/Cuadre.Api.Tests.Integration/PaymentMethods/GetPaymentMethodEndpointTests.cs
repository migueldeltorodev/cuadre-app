using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.PaymentMethods;

[Collection(nameof(ApiCollection))]
public class GetPaymentMethodEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Get_ShouldReturnPaymentMethod_WhenItExists()
    {
        // Arrange
        var all = await Client.GetFromJsonAsync<PaymentMethodsResponse>(ApiEndpoints.PaymentMethods.GetAll, Ct);
        var enzona = all!.Items.Single(x => x.Name == "Enzona");

        // Act
        var response = await Client.GetAsync(Route(ApiEndpoints.PaymentMethods.Get, enzona.Id), Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaymentMethodResponse>(Ct);
        body!.Should().BeEquivalentTo(enzona);
    }

    [Fact]
    public async Task Get_ShouldReturnNotFound_WhenPaymentMethodDoesNotExist()
    {
        // Act
        var response = await Client.GetAsync(Route(ApiEndpoints.PaymentMethods.Get, Guid.NewGuid()), Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
