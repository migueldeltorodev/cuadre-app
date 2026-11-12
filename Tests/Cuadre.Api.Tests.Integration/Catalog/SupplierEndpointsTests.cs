using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Fakers;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Catalog;

[Collection(nameof(ApiCollection))]
public class SupplierEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Create_ShouldReturnCreated_WithLocation()
    {
        var request = SupplierFaker.CreateRequest();

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Suppliers.Create, request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<SupplierResponse>(Ct);
        created!.Name.Should().Be(request.Name);
        created.Phone.Should().Be(request.Phone);
        created.IsActive.Should().BeTrue();
        response.Headers.Location!.ToString().Should().EndWith($"/api/suppliers/{created.Id}");
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenNameExistsIgnoringCase()
    {
        var existing = await Client.CreateSupplierAsync(Ct);
        var request = new CreateSupplierRequest { Name = existing.Name.ToUpperInvariant() };

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Suppliers.Create, request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenNameIsBlank()
    {
        var request = new CreateSupplierRequest { Name = "  " };

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Suppliers.Create, request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ShouldDeactivate_AndClearBlankFields()
    {
        // Arrange
        var supplier = await Client.CreateSupplierAsync(Ct);
        var request = new UpdateSupplierRequest { Name = supplier.Name, Phone = " ", IsActive = false };

        // Act
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Suppliers.Update, supplier.Id), request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<SupplierResponse>(Ct);
        updated!.IsActive.Should().BeFalse();
        updated.Phone.Should().BeNull();
        updated.CreatedAt.Should().Be(supplier.CreatedAt);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenSupplierDoesNotExist()
    {
        var request = new UpdateSupplierRequest { Name = "Nadie", IsActive = true };

        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Suppliers.Update, Guid.NewGuid()), request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_ShouldListActiveFirst()
    {
        var inactive = await Client.CreateSupplierAsync(Ct);
        await Client.PutAsJsonAsync(Route(ApiEndpoints.Suppliers.Update, inactive.Id),
            new UpdateSupplierRequest { Name = inactive.Name, IsActive = false }, Ct);
        var active = await Client.CreateSupplierAsync(Ct);

        var all = await Client.GetFromJsonAsync<SuppliersResponse>(ApiEndpoints.Suppliers.GetAll, Ct);

        all!.Items.Select(s => s.Id).Should().Equal(active.Id, inactive.Id);
    }
}
