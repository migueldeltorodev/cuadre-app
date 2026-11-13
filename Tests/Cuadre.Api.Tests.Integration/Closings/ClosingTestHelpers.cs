using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Closings;

internal static class ClosingTestHelpers
{
    public static async Task<DailyClosingDraftResponse> GetDraftAsync(this HttpClient client, DateOnly date,
        CancellationToken ct) =>
        (await client.GetFromJsonAsync<DailyClosingDraftResponse>(
            $"{ApiEndpoints.DailyClosings.Draft}?date={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            ct))!;

    public static ClosingCountRequest Counted(Guid productId, Guid locationId, long units) => new()
    {
        ProductId = productId,
        LocationId = locationId,
        CountedMilli = units * 1_000
    };

    public static ClosingCollectionRequest Collected(this DailyClosingDraftResponse draft, string methodName,
        long amountCents) => new()
    {
        PaymentMethodId = draft.PaymentMethods.Single(x => x.Name == methodName).Id,
        AmountCents = amountCents
    };

    public static async Task<DailyClosingResponse> CloseAsync(this HttpClient client,
        CreateDailyClosingRequest request, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(ApiEndpoints.DailyClosings.Create, request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
        return (await response.Content.ReadFromJsonAsync<DailyClosingResponse>(ct))!;
    }

    public static DailyClosingAllocationResponse Allocation(this DailyClosingResponse closing,
        EnvelopeType role) =>
        closing.Allocations.Single(x => x.Role == role);
}
