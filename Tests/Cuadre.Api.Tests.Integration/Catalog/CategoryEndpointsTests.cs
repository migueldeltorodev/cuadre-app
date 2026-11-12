using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Fakers;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Catalog;

[Collection(nameof(ApiCollection))]
public class CategoryEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Create_ShouldDerivePrefixFromName_WhenNoneIsGiven()
    {
        // Act
        var created = await Client.CreateCategoryAsync("Café", Ct);

        // Assert
        created.CodePrefix.Should().Be("CAF");
        created.ParentId.Should().BeNull();
        created.ProductCount.Should().Be(0);
    }

    [Fact]
    public async Task Create_ShouldUppercaseTheTypedPrefix()
    {
        var created = await Client.CreateCategoryAsync("Granos", Ct, codePrefix: "grn");

        created.CodePrefix.Should().Be("GRN");
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenDerivedPrefixIsTaken()
    {
        // Arrange — "Arroz" takes ARR; "Arroz integral" would derive ARR too
        await Client.CreateCategoryAsync("Arroz", Ct);
        var request = new CreateCategoryRequest { Name = "Arroz integral" };

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Categories.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenTopLevelNameExistsIgnoringCase()
    {
        // The unique index uses COALESCE(parent_id, ''): two NULL parents must still collide.
        await Client.CreateCategoryAsync("Bebidas", Ct, codePrefix: "BEB");
        var request = new CreateCategoryRequest { Name = "BEBIDAS", CodePrefix = "BBD" };

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Categories.Create, request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_ShouldAllowSameSubcategoryName_UnderDifferentCategories()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var beans = await Client.CreateCategoryAsync("Frijoles", Ct);

        await Client.CreateCategoryAsync("Importado", Ct, parentId: rice.Id);
        var second = await Client.CreateCategoryAsync("Importado", Ct, parentId: beans.Id);

        second.ParentId.Should().Be(beans.Id);
        second.CodePrefix.Should().BeNull();
    }

    [Fact]
    public async Task Create_ShouldReturnUnprocessable_WhenParentIsASubcategory()
    {
        // Arrange — two levels only
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var imported = await Client.CreateCategoryAsync("Importado", Ct, parentId: rice.Id);
        var request = new CreateCategoryRequest { Name = "Guyanés", ParentId = imported.Id };

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Categories.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenSubcategoryHasPrefix()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var request = new CreateCategoryRequest { Name = "Importado", ParentId = rice.Id, CodePrefix = "IMP" };

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Categories.Create, request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ValidationFailureResponse>(Ct);
        body!.Errors.Should().ContainSingle(e => e.PropertyName == "CodePrefix");
    }

    [Fact]
    public async Task GetAll_ShouldListTopLevelFirst_WithProductCounts()
    {
        // Arrange
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var imported = await Client.CreateCategoryAsync("Importado", Ct, parentId: rice.Id);
        await Client.CreateCategoryAsync("Bebidas", Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id, imported.Id), Ct);

        // Act
        var all = await Client.GetFromJsonAsync<CategoriesResponse>(ApiEndpoints.Categories.GetAll, Ct);

        // Assert
        all!.Items.Select(c => c.Name).Should().Equal("Arroz", "Bebidas", "Importado");
        all.Items.Single(c => c.Id == rice.Id).ProductCount.Should().Be(1);
        all.Items.Single(c => c.Id == imported.Id).ProductCount.Should().Be(1);
    }

    [Fact]
    public async Task Update_ShouldRenameAndKeepPrefix_WhenPrefixIsBlank()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var request = new UpdateCategoryRequest { Name = "Arroces", CodePrefix = "" };

        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Categories.Update, rice.Id), request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<CategoryResponse>(Ct);
        updated!.Name.Should().Be("Arroces");
        updated.CodePrefix.Should().Be("ARR");
    }

    [Fact]
    public async Task Delete_ShouldReturnNoContent_WhenUnused()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);

        var response = await Client.DeleteAsync(Route(ApiEndpoints.Categories.Delete, rice.Id), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync(Route(ApiEndpoints.Categories.Get, rice.Id), Ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ShouldReturnUnprocessable_WhenItHasSubcategories()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        await Client.CreateCategoryAsync("Importado", Ct, parentId: rice.Id);

        var response = await Client.DeleteAsync(Route(ApiEndpoints.Categories.Delete, rice.Id), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Delete_ShouldReturnUnprocessable_WhenProductsUseIt()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);

        var response = await Client.DeleteAsync(Route(ApiEndpoints.Categories.Delete, rice.Id), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_WhenCategoryDoesNotExist()
    {
        var response = await Client.DeleteAsync(Route(ApiEndpoints.Categories.Delete, Guid.NewGuid()), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
