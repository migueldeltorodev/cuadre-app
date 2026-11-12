using System.Net.Http.Json;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Tests.Integration.CashBoxes;

internal static class CashBoxTestHelpers
{
    /// <summary>The seeded boxes get new ids on every reset, so tests find them by name.</summary>
    public static async Task<CashBoxResponse> GetCashBoxByNameAsync(this HttpClient client, string name,
        CancellationToken ct)
    {
        var all = await client.GetFromJsonAsync<CashBoxesResponse>(ApiEndpoints.CashBoxes.GetAll, ct);
        return all!.Items.Single(x => x.Name == name);
    }
}
