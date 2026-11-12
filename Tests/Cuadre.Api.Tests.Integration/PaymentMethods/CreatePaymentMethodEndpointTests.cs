using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.CashBoxes;
using Cuadre.Api.Tests.Integration.Fakers;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.PaymentMethods;

[Collection(nameof(ApiCollection))]
public class CreatePaymentMethodEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Create_ShouldReturnCreated_WhenCashBoxIsActive()
    {
        // Arrange
        var bank = await Client.GetCashBoxByNameAsync("Banco CUP", Ct);
        var request = PaymentMethodFaker.CreateRequest(bank.Id);

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.PaymentMethods.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<PaymentMethodResponse>(Ct);
        created!.Name.Should().Be(request.Name);
        created.CashBoxId.Should().Be(bank.Id);
        created.IsActive.Should().BeTrue();
        response.Headers.Location!.ToString().Should().EndWith($"/api/payment-methods/{created.Id}");
    }

    [Fact]
    public async Task Create_ShouldReturnUnprocessable_WhenCashBoxDoesNotExist()
    {
        // Arrange
        var request = PaymentMethodFaker.CreateRequest(Guid.NewGuid());

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.PaymentMethods.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturnUnprocessable_WhenCashBoxIsInactive()
    {
        // Arrange — "Efectivo USD" has no payment methods, so it can be deactivated
        var usd = await Client.GetCashBoxByNameAsync("Efectivo USD", Ct);
        var deactivate = new UpdateCashBoxRequest { Name = usd.Name, IsActive = false };
        (await Client.PutAsJsonAsync(Route(ApiEndpoints.CashBoxes.Update, usd.Id), deactivate, Ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var request = PaymentMethodFaker.CreateRequest(usd.Id);

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.PaymentMethods.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenNameExistsIgnoringCase()
    {
        // Arrange — "Enzona" is seeded
        var bank = await Client.GetCashBoxByNameAsync("Banco CUP", Ct);
        var request = new CreatePaymentMethodRequest { Name = "ENZONA", CashBoxId = bank.Id };

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.PaymentMethods.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenCashBoxIsMissing()
    {
        // Arrange
        var request = new CreatePaymentMethodRequest { Name = "Sin caja", CashBoxId = Guid.Empty };

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.PaymentMethods.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ValidationFailureResponse>(Ct);
        body!.Errors.Should().ContainSingle(e => e.PropertyName == "CashBoxId");
    }
}
