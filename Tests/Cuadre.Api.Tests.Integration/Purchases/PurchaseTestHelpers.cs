using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.CashBoxes;
using Cuadre.Api.Tests.Integration.Catalog;
using Cuadre.Api.Tests.Integration.Fakers;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Purchases;

internal static class PurchaseTestHelpers
{
    public const long Rate325 = 32_500;

    /// <summary>A rice-like product (100 lb sack → lb, sale price 250 CUP) and an active supplier.</summary>
    public static async Task<(ProductResponse Product, SupplierResponse Supplier)> CreateRiceAsync(
        this HttpClient client, CancellationToken ct, long? salePriceCents = 25_000)
    {
        var category = await client.CreateCategoryAsync($"Arroz {Guid.NewGuid():N}"[..14], ct,
            codePrefix: RandomPrefix());
        var supplier = await client.CreateSupplierAsync(ct);
        var product = await client.CreateProductAsync(
            ProductFaker.CreateRequest(category.Id, salePriceCents: salePriceCents), ct);
        return (product, supplier);
    }

    /// <summary>A USD purchase paid from the seeded "Efectivo USD" box, one line in sacks.</summary>
    public static async Task<CreatePurchaseRequest> UsdPurchaseRequestAsync(this HttpClient client,
        Guid supplierId, DateOnly date, CancellationToken ct, params CreatePurchaseLineRequest[] lines)
    {
        var usdBox = await client.GetCashBoxByNameAsync("Efectivo USD", ct);
        return new CreatePurchaseRequest
        {
            Date = date,
            SupplierId = supplierId,
            Currency = CurrencyCode.USD,
            RateCupPerUsdCents = Rate325,
            CashBoxId = usdBox.Id,
            Lines = lines
        };
    }

    public static CreatePurchaseLineRequest Sacks(Guid productId, long sacks, long usdCentsPerSack) => new()
    {
        ProductId = productId,
        QuantityMilli = sacks * 1_000,
        UnitCostCents = usdCentsPerSack
    };

    public static async Task<PurchaseResponse> CreatePurchaseAsync(this HttpClient client,
        CreatePurchaseRequest request, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(ApiEndpoints.Purchases.Create, request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
        return (await response.Content.ReadFromJsonAsync<PurchaseResponse>(ct))!;
    }

    private static string RandomPrefix() =>
        new string(Enumerable.Range(0, 4).Select(_ => (char)('A' + Random.Shared.Next(26))).ToArray());
}
