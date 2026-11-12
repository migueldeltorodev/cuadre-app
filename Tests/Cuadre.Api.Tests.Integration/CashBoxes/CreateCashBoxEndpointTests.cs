using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Fakers;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.CashBoxes;

[Collection(nameof(ApiCollection))]
public class CreateCashBoxEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Create_ShouldReturnCreated_WhenRequestIsValid()
    {
        // Arrange
        var request = CashBoxFaker.CreateRequest().Generate();

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.CashBoxes.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CashBoxResponse>(Ct);
        created!.Name.Should().Be(request.Name);
        created.Currency.Should().Be(request.Currency);
        created.Kind.Should().Be(request.Kind);
        created.IsActive.Should().BeTrue();
        created.CreatedAt.Should().Be(Clock.Now.UtcDateTime);
        response.Headers.Location!.ToString().Should().EndWith($"/api/cash-boxes/{created.Id}");
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenNameExistsIgnoringCase()
    {
        // Arrange — "Efectivo CUP" is seeded
        var request = new CreateCashBoxRequest
        {
            Name = "efectivo cup",
            Currency = CurrencyCode.CUP,
            Kind = CashBoxType.Cash
        };

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.CashBoxes.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        var request = new CreateCashBoxRequest { Name = "", Currency = CurrencyCode.CUP, Kind = CashBoxType.Cash };

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.CashBoxes.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ValidationFailureResponse>(Ct);
        body!.Errors.Should().ContainSingle(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenCurrencyIsUnknown()
    {
        // Arrange — raw JSON: a typed request cannot express an undefined currency
        var json = """{ "name": "Caja rara", "currency": 7, "kind": "Cash" }""";
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync(ApiEndpoints.CashBoxes.Create, content, Ct);

        // Assert — must be a controlled 400, never a 500
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
