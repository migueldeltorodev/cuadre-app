using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.CashBoxes;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.PaymentMethods;

[Collection(nameof(ApiCollection))]
public class GetAllPaymentMethodsEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAll_ShouldReturnFactoryMethods_LinkedToTheirCashBoxes()
    {
        // Arrange
        var cash = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        var bank = await Client.GetCashBoxByNameAsync("Banco CUP", Ct);

        // Act
        var response = await Client.GetAsync(ApiEndpoints.PaymentMethods.GetAll, Ct);

        // Assert — transfers land in the bank, cash in the cash box (RF-CAJ-02)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaymentMethodsResponse>(Ct);
        body!.Items.Select(x => (x.Name, x.CashBoxId)).Should().BeEquivalentTo([
            ("Efectivo", cash.Id),
            ("Transfermóvil", bank.Id),
            ("Enzona", bank.Id)
        ]);
    }
}
