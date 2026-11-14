using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Api.Tests.Integration.Inventory;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;
using static Cuadre.Api.Tests.Integration.Closings.ClosingTestHelpers;
using static Cuadre.Api.Tests.Integration.Inventory.InventoryTestHelpers;
using static Cuadre.Api.Tests.Integration.Purchases.PurchaseTestHelpers;

namespace Cuadre.Api.Tests.Integration.Purchases;

// D-61. Seeded settings: colchón 0, recargo 0, rounding step 1 CUP → suggested price = CUP cost rounded UP to the peso.
// Sacks of 100 lb at 325: 60 USD = 195 CUP/lb, 70 USD = 227.50 → 228.

[Collection(nameof(ApiCollection))]
public class PriceNoticeEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<ProductResponse> TwoSacksAsync(long salePriceCents)
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct, salePriceCents);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 7_000)), Ct);
        return rice;
    }

    private async Task<IReadOnlyList<PriceNoticeResponse>> NoticesAsync() =>
        (await Client.GetFromJsonAsync<PriceNoticesResponse>(ApiEndpoints.Products.PriceNotices, Ct))!.Items;

    private async Task<ProductResponse> ResolveAsync(Guid productId, PriceNoticeKindCode kind, bool accept)
    {
        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.Products.ResolvePriceNotice, productId),
            new ResolvePriceNoticeRequest { Kind = kind, AcceptSuggested = accept }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(Ct))!;
    }

    private async Task SetPriceAsync(Guid productId, long priceCents)
    {
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.SalePrice, productId),
            new SetSalePriceRequest { SalePriceCents = priceCents }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The first sack goes to the counter and all of it is sold: the second one goes on sale.</summary>
    private async Task SellTheFirstSackAsync(Guid productId, long collectedCents)
    {
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);
        await Client.TransferAsync(Clock.Today, storage.Id, counter.Id, Ct, SacksOf(productId, 1));
        var draft = await Client.GetDraftAsync(Clock.Today, Ct);
        await Client.CloseAsync(new CreateDailyClosingRequest
        {
            Date = Clock.Today,
            Counts = [Counted(productId, counter.Id, 0)],
            Collections = [draft.Collected("Efectivo", collectedCents)]
        }, Ct);
    }

    [Fact]
    public async Task Pricing_ShouldSuggestFromTheBatchBeingSold_AndShowWhatTheNewestNeeds()
    {
        var rice = await TwoSacksAsync(25_000);

        var product = await Client.GetFromJsonAsync<ProductResponse>(Route(ApiEndpoints.Products.Get, rice.Id), Ct);

        product!.Cost!.CurrentCostCupMicros.Should().Be(195_000_000);
        product.Cost.CurrentRateCents.Should().Be(32_500);
        product.Cost.SuggestedPriceCents.Should().Be(19_500);
        product.Cost.NewestSuggestedPriceCents.Should().Be(22_800);
        product.Cost.MarginBps.Should().Be(2_200);   // (250 − 195) / 250
    }

    [Fact]
    public async Task Notices_ShouldWarnEarly_WhenTheNextBatchWillNotFitInThePrice()
    {
        // Price 200 decided for the first sack: it covers 195, but not the 228 the second one needs.
        var rice = await TwoSacksAsync(25_000);
        await SetPriceAsync(rice.Id, 20_000);

        var notice = (await NoticesAsync()).Should().ContainSingle().Subject;

        notice.Kind.Should().Be(PriceNoticeKindCode.NewBatchCostlier);
        notice.ProductId.Should().Be(rice.Id);
        notice.SalePriceCents.Should().Be(20_000);
        notice.SuggestedPriceCents.Should().Be(22_800);
        notice.BatchCostCupMicros.Should().Be(227_500_000);

        // "Esperar": the warning goes away for this batch; the price stays.
        var kept = await ResolveAsync(rice.Id, PriceNoticeKindCode.NewBatchCostlier, accept: false);

        kept.SalePriceCents.Should().Be(20_000);
        (await NoticesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Notices_ShouldTellWhenANewBatchGoesOnSale_AndTakeItsPrice()
    {
        // Price 200 for the first sack, the warning dismissed; then the whole first sack is sold.
        var rice = await TwoSacksAsync(25_000);
        await SetPriceAsync(rice.Id, 20_000);
        await ResolveAsync(rice.Id, PriceNoticeKindCode.NewBatchCostlier, accept: false);
        await SellTheFirstSackAsync(rice.Id, 2_000_000);

        var notice = (await NoticesAsync()).Should().ContainSingle().Subject;

        notice.Kind.Should().Be(PriceNoticeKindCode.BatchChanged);
        notice.SuggestedPriceCents.Should().Be(22_800);
        notice.BatchCostCupMicros.Should().Be(227_500_000);

        // "Ya lo cambié": the suggested price is in force from today and the notice goes away.
        var accepted = await ResolveAsync(rice.Id, PriceNoticeKindCode.BatchChanged, accept: true);

        accepted.SalePriceCents.Should().Be(22_800);
        (await NoticesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Notices_ShouldStayQuiet_WhenThePriceIsKeptForTheNewBatch()
    {
        var rice = await TwoSacksAsync(25_000);
        await SetPriceAsync(rice.Id, 20_000);
        await ResolveAsync(rice.Id, PriceNoticeKindCode.NewBatchCostlier, accept: false);
        await SellTheFirstSackAsync(rice.Id, 2_000_000);

        var kept = await ResolveAsync(rice.Id, PriceNoticeKindCode.BatchChanged, accept: false);

        kept.SalePriceCents.Should().Be(20_000);
        (await NoticesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Notices_ShouldAskToReviewThePrice_OfTheFirstBatchOfANewProduct()
    {
        // A product priced at 250 before ever buying it: its first batch suggests 195.
        var (rice, supplier) = await Client.CreateRiceAsync(Ct, 25_000);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);

        var notice = (await NoticesAsync()).Should().ContainSingle().Subject;

        notice.Kind.Should().Be(PriceNoticeKindCode.BatchChanged);
        notice.SuggestedPriceCents.Should().Be(19_500);
    }
}
