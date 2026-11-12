using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Fakers;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Catalog;

[Collection(nameof(ApiCollection))]
public class PriceListEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Get_ShouldGroupByCategory_AndFlagNeverCopiedPrices()
    {
        // Arrange
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var drinks = await Client.CreateCategoryAsync("Bebidas", Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(drinks.Id), Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id, salePriceCents: null), Ct);

        // Act
        var list = await Client.GetFromJsonAsync<PriceListResponse>(ApiEndpoints.PriceList.Get, Ct);

        // Assert
        list!.LastCopy.Should().BeNull();
        list.Groups.Select(g => g.CategoryName).Should().Equal("Arroz", "Bebidas");
        list.Groups.First().Entries.Should().HaveCount(2);
        list.ChangedCount.Should().Be(2);   // the unpriced one is listed but not "changed"
    }

    [Fact]
    public async Task MarkCopied_ShouldSnapshotPrices_SoNothingIsFlaggedAfterwards()
    {
        // Arrange
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);

        // Act
        var response = await Client.PostAsync(ApiEndpoints.PriceList.Copies, null, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var copy = await response.Content.ReadFromJsonAsync<PriceListCopyResponse>(Ct);
        copy!.ProductCount.Should().Be(2);
        copy.ChangedCount.Should().Be(2);

        var list = await Client.GetFromJsonAsync<PriceListResponse>(ApiEndpoints.PriceList.Get, Ct);
        list!.ChangedCount.Should().Be(0);
        list.LastCopy!.Id.Should().Be(copy.Id);
    }

    [Fact]
    public async Task Get_ShouldFlagOnlyPricesChangedAfterTheLastCopy()
    {
        // Arrange
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var changed = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);
        await Client.PostAsync(ApiEndpoints.PriceList.Copies, null, Ct);

        // Act
        await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.Update, changed.Id),
            ProductFaker.UpdateRequest(changed, salePriceCents: 30_000), Ct);
        var list = await Client.GetFromJsonAsync<PriceListResponse>(ApiEndpoints.PriceList.Get, Ct);

        // Assert
        list!.ChangedCount.Should().Be(1);
        var entry = list.Groups.Single().Entries.Single(e => e.IsChanged);
        entry.ProductId.Should().Be(changed.Id);
        entry.SalePriceCents.Should().Be(30_000);
        entry.ListedPriceCents.Should().Be(25_000);   // what the sign still says
    }

    [Fact]
    public async Task Get_ShouldLeaveOutInactiveProducts()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var product = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);
        await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.Update, product.Id),
            ProductFaker.UpdateRequest(product, isActive: false), Ct);

        var list = await Client.GetFromJsonAsync<PriceListResponse>(ApiEndpoints.PriceList.Get, Ct);

        list!.Groups.Should().BeEmpty();
    }
}
