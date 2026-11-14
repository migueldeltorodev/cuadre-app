using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Taxes;

// D-62: taxes only suggest how much to set aside. None by default.

[Collection(nameof(ApiCollection))]
public class TaxEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<HttpResponseMessage> CreateAsync(string name, int rateBps, TaxBaseCode taxBase) =>
        await Client.PostAsJsonAsync(ApiEndpoints.Taxes.Create,
            new CreateTaxRequest { Name = name, RateBps = rateBps, Base = taxBase }, Ct);

    private async Task<TaxesResponse> GetAllAsync() =>
        (await Client.GetFromJsonAsync<TaxesResponse>(ApiEndpoints.Taxes.GetAll, Ct))!;

    [Fact]
    public async Task GetAll_ShouldBeEmpty_ByDefault()
    {
        (await GetAllAsync()).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ShouldAddAnActiveTax()
    {
        var response = await CreateAsync("ONAT", 1_000, TaxBaseCode.Sales);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var tax = (await response.Content.ReadFromJsonAsync<TaxResponse>(Ct))!;
        tax.Name.Should().Be("ONAT");
        tax.RateBps.Should().Be(1_000);
        tax.Base.Should().Be(TaxBaseCode.Sales);
        tax.IsActive.Should().BeTrue();
        (await GetAllAsync()).Items.Should().ContainSingle().Which.Id.Should().Be(tax.Id);
    }

    [Fact]
    public async Task Create_ShouldRefuseTheSameName_IgnoringCase()
    {
        await CreateAsync("ONAT", 1_000, TaxBaseCode.Sales);

        var response = await CreateAsync("onat", 500, TaxBaseCode.Profit);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public async Task Create_ShouldRefuseARateOutOfRange(int rateBps)
    {
        var response = await CreateAsync("ONAT", rateBps, TaxBaseCode.Sales);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ShouldChangeTheRateAndTheBase_AndDeactivate()
    {
        var created = (await (await CreateAsync("ONAT", 1_000, TaxBaseCode.Sales))
            .Content.ReadFromJsonAsync<TaxResponse>(Ct))!;

        var response = await Client.PutAsJsonAsync(ApiEndpoints.Taxes.Update.Replace("{id:guid}", created.Id.ToString()),
            new UpdateTaxRequest { Name = "ONAT", RateBps = 3_500, Base = TaxBaseCode.Profit, IsActive = false }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var updated = (await response.Content.ReadFromJsonAsync<TaxResponse>(Ct))!;
        updated.RateBps.Should().Be(3_500);
        updated.Base.Should().Be(TaxBaseCode.Profit);
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ShouldBeNotFound_ForAnUnknownTax()
    {
        var response = await Client.PutAsJsonAsync(ApiEndpoints.Taxes.Update.Replace("{id:guid}", Guid.NewGuid().ToString()),
            new UpdateTaxRequest { Name = "ONAT", RateBps = 1_000, Base = TaxBaseCode.Sales, IsActive = true }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
