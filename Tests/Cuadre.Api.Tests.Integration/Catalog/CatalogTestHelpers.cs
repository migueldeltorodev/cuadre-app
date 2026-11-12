using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Fakers;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Catalog;

/// <summary>Arrange helpers: each one asserts its own success so a broken arrange fails loudly, not later.</summary>
internal static class CatalogTestHelpers
{
    public static async Task<CategoryResponse> CreateCategoryAsync(this HttpClient client, string name,
        CancellationToken ct, Guid? parentId = null, string? codePrefix = null)
    {
        var request = new CreateCategoryRequest { Name = name, ParentId = parentId, CodePrefix = codePrefix };
        var response = await client.PostAsJsonAsync(ApiEndpoints.Categories.Create, request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>(ct))!;
    }

    public static async Task<SupplierResponse> CreateSupplierAsync(this HttpClient client, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(ApiEndpoints.Suppliers.Create, SupplierFaker.CreateRequest(), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
        return (await response.Content.ReadFromJsonAsync<SupplierResponse>(ct))!;
    }

    public static async Task<ProductResponse> CreateProductAsync(this HttpClient client,
        CreateProductRequest request, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(ApiEndpoints.Products.Create, request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(ct))!;
    }
}
