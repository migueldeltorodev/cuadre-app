using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.CashBoxes;

[Collection(nameof(ApiCollection))]
public class GetAllCashBoxesEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAll_ShouldReturnFactoryCashBoxes_OnFreshInstall()
    {
        // Act
        var response = await Client.GetAsync(ApiEndpoints.CashBoxes.GetAll, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CashBoxesResponse>(Ct);
        body!.Items.Select(x => (x.Name, x.Currency, x.Kind)).Should().BeEquivalentTo([
            ("Efectivo CUP", CurrencyCode.CUP, CashBoxType.Cash),
            ("Banco CUP", CurrencyCode.CUP, CashBoxType.Bank),
            ("Efectivo USD", CurrencyCode.USD, CashBoxType.Cash)
        ]);
        body.Items.Should().OnlyContain(x => x.IsActive);
    }
}
