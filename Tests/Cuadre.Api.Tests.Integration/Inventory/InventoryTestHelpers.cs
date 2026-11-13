using System.Net;
using System.Net.Http.Json;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Inventory;

internal static class InventoryTestHelpers
{
    /// <summary>The seeded Almacén (Storage) and Mostrador (Counter).</summary>
    public static async Task<(LocationResponse Storage, LocationResponse Counter)> GetSeededLocationsAsync(
        this HttpClient client, CancellationToken ct)
    {
        var locations = (await client.GetFromJsonAsync<LocationsResponse>(ApiEndpoints.Locations.GetAll, ct))!;
        return (locations.Items.Single(x => x.Kind == LocationType.Storage && x.IsActive),
            locations.Items.Single(x => x.Kind == LocationType.Counter && x.IsActive));
    }

    public static StockOperationLineRequest Pounds(Guid productId, long pounds) => new()
    {
        ProductId = productId,
        QuantityMilli = pounds * 1_000,
        InPurchaseUnits = false
    };

    public static StockOperationLineRequest SacksOf(Guid productId, long sacks) => new()
    {
        ProductId = productId,
        QuantityMilli = sacks * 1_000,
        InPurchaseUnits = true
    };

    public static async Task<StockOperationResponse> TransferAsync(this HttpClient client, DateOnly date,
        Guid fromId, Guid toId, CancellationToken ct, params StockOperationLineRequest[] lines) =>
        await client.PostOperationAsync(ApiEndpoints.StockOperations.CreateTransfer, new CreateTransferRequest
        {
            Date = date,
            FromLocationId = fromId,
            ToLocationId = toId,
            Lines = lines
        }, ct);

    public static async Task<StockOperationResponse> ExitAsync(this HttpClient client, DateOnly date,
        Guid locationId, ExitReasonCode reason, CancellationToken ct, params StockOperationLineRequest[] lines) =>
        await client.PostOperationAsync(ApiEndpoints.StockOperations.CreateExit, new CreateExitRequest
        {
            Date = date,
            LocationId = locationId,
            Reason = reason,
            Lines = lines
        }, ct);

    public static async Task<StockOperationResponse> CountAsync(this HttpClient client, DateOnly date,
        Guid locationId, CancellationToken ct, params StockOperationLineRequest[] lines) =>
        await client.PostOperationAsync(ApiEndpoints.StockOperations.CreateCount, new CreateCountRequest
        {
            Date = date,
            LocationId = locationId,
            Lines = lines
        }, ct);

    public static async Task<StockResponse> GetStockAsync(this HttpClient client, CancellationToken ct,
        string query = "") =>
        (await client.GetFromJsonAsync<StockResponse>(ApiEndpoints.Inventory.Stock + query, ct))!;

    /// <summary>The quantity of a product at a location in the stock table (0 when absent).</summary>
    public static long QuantityAt(this StockResponse stock, Guid productId, Guid locationId) =>
        stock.Items.Single(x => x.ProductId == productId).Locations
            .SingleOrDefault(x => x.LocationId == locationId)?.QuantityMilli ?? 0;

    private static async Task<StockOperationResponse> PostOperationAsync<TRequest>(this HttpClient client,
        string route, TRequest request, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(route, request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
        return (await response.Content.ReadFromJsonAsync<StockOperationResponse>(ct))!;
    }
}
