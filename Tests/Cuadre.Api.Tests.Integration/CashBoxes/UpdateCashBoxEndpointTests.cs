using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Cuadre.Api.Tests.Integration.CashBoxes;

[Collection(nameof(ApiCollection))]
public class UpdateCashBoxEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Update_ShouldRenameAndDeactivate_WhenNoActivePaymentMethodUsesIt()
    {
        // Arrange — no seeded payment method goes to "Efectivo USD"
        var usd = await Client.GetCashBoxByNameAsync("Efectivo USD", Ct);
        Clock.Now = Clock.Now.AddHours(1);
        var request = new UpdateCashBoxRequest { Name = "Dólares en casa", IsActive = false };

        // Act
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.CashBoxes.Update, usd.Id), request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<CashBoxResponse>(Ct);
        updated!.Name.Should().Be("Dólares en casa");
        updated.IsActive.Should().BeFalse();
        updated.Currency.Should().Be(CurrencyCode.USD, "currency is immutable");
        updated.CreatedAt.Should().Be(usd.CreatedAt);
        updated.UpdatedAt.Should().Be(Clock.Now.UtcDateTime);
    }

    [Fact]
    public async Task Update_ShouldReturnUnprocessable_WhenDeactivatingBoxUsedByActivePaymentMethod()
    {
        // Arrange — the seeded "Efectivo" payment method lands in "Efectivo CUP"
        var cash = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        var request = new UpdateCashBoxRequest { Name = cash.Name, IsActive = false };

        // Act
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.CashBoxes.Update, cash.Id), request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        problem!.Title.Should().Contain("formas de cobro activas");
    }

    [Fact]
    public async Task Update_ShouldReturnConflict_WhenNameBelongsToAnotherBox()
    {
        // Arrange
        var usd = await Client.GetCashBoxByNameAsync("Efectivo USD", Ct);
        var request = new UpdateCashBoxRequest { Name = "BANCO cup", IsActive = true };

        // Act
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.CashBoxes.Update, usd.Id), request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenCashBoxDoesNotExist()
    {
        // Arrange
        var request = new UpdateCashBoxRequest { Name = "Lo que sea", IsActive = true };

        // Act
        var response = await Client.PutAsJsonAsync(
            Route(ApiEndpoints.CashBoxes.Update, Guid.NewGuid()), request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
