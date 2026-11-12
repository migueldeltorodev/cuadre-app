using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.CashBoxes;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.PaymentMethods;

[Collection(nameof(ApiCollection))]
public class UpdatePaymentMethodEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Update_ShouldMoveToAnotherBoxAndDeactivate_WhenRequestIsValid()
    {
        // Arrange
        var all = await Client.GetFromJsonAsync<PaymentMethodsResponse>(ApiEndpoints.PaymentMethods.GetAll, Ct);
        var enzona = all!.Items.Single(x => x.Name == "Enzona");
        var cash = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        var request = new UpdatePaymentMethodRequest { Name = "Enzona (vieja)", CashBoxId = cash.Id, IsActive = false };

        // Act
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.PaymentMethods.Update, enzona.Id), request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<PaymentMethodResponse>(Ct);
        updated!.Name.Should().Be("Enzona (vieja)");
        updated.CashBoxId.Should().Be(cash.Id);
        updated.IsActive.Should().BeFalse();
        updated.CreatedAt.Should().Be(enzona.CreatedAt);
    }

    [Fact]
    public async Task Update_ShouldFreeTheCashBox_SoItCanBeDeactivated()
    {
        // Arrange — Transfermóvil and Enzona both pay into "Banco CUP"; deactivate both
        var bank = await Client.GetCashBoxByNameAsync("Banco CUP", Ct);
        var all = await Client.GetFromJsonAsync<PaymentMethodsResponse>(ApiEndpoints.PaymentMethods.GetAll, Ct);
        foreach (var method in all!.Items.Where(x => x.CashBoxId == bank.Id))
        {
            var off = new UpdatePaymentMethodRequest { Name = method.Name, CashBoxId = bank.Id, IsActive = false };
            (await Client.PutAsJsonAsync(Route(ApiEndpoints.PaymentMethods.Update, method.Id), off, Ct))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Act
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.CashBoxes.Update, bank.Id),
            new UpdateCashBoxRequest { Name = bank.Name, IsActive = false }, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenPaymentMethodDoesNotExist()
    {
        // Arrange
        var cash = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        var request = new UpdatePaymentMethodRequest { Name = "Fantasma", CashBoxId = cash.Id, IsActive = true };

        // Act
        var response = await Client.PutAsJsonAsync(
            Route(ApiEndpoints.PaymentMethods.Update, Guid.NewGuid()), request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
