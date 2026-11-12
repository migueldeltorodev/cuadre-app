using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.CashBoxes;
using Cuadre.Api.Tests.Integration.Fakers;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;
using static Cuadre.Api.Tests.Integration.Purchases.PurchaseTestHelpers;

namespace Cuadre.Api.Tests.Integration.Purchases;

// Seeded settings: colchón 0, recargo 0, rounding step 1 CUP → suggested price = CUP cost rounded UP to the peso.

[Collection(nameof(ApiCollection))]
public class PurchaseEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    // ---------- Create ----------

    [Fact]
    public async Task Create_ShouldReturnCreated_WithLandedCostsInTheDefaultStorage()
    {
        // Arrange
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var request = await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 2, 6_200));
        var locations = await Client.GetFromJsonAsync<LocationsResponse>(ApiEndpoints.Locations.GetAll, Ct);

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Purchases.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var created = (await response.Content.ReadFromJsonAsync<PurchaseResponse>(Ct))!;
        created.Number.Should().Be(1);
        created.Status.Should().Be(PurchaseState.Confirmed);
        created.SupplierName.Should().Be(supplier.Name);
        created.GoodsTotalCents.Should().Be(12_400);
        created.LocationId.Should().Be(locations!.Items.Single(x => x.Kind == LocationType.Storage).Id);

        var line = created.Lines.Should().ContainSingle().Subject;
        line.ProductName.Should().Be(rice.Name);
        line.PurchaseUnit.Should().Be(UnitCode.Sack);
        line.StockUnit.Should().Be(UnitCode.Pound);
        line.ConversionFactorMilli.Should().Be(100_000);
        line.StockQuantityMilli.Should().Be(200_000);            // 2 sacks × 100 lb
        line.LandedUnitCostUsdMicros.Should().Be(620_000);       // 124 USD / 200 lb
        line.LandedUnitCostCupMicros.Should().Be(201_500_000);   // × 325

        response.Headers.Location!.ToString().Should().EndWith($"/api/purchases/{created.Id}");
    }

    [Fact]
    public async Task Create_ShouldNumberPurchasesConsecutively()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var request = await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000));

        var first = await Client.CreatePurchaseAsync(request, Ct);
        var second = await Client.CreatePurchaseAsync(request, Ct);

        first.Number.Should().Be(1);
        second.Number.Should().Be(2);
    }

    [Fact]
    public async Task Create_ShouldUpdateStockCostAndSuggestedPrice_OfTheProduct()
    {
        // Arrange
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var request = await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 2, 6_200));

        // Act
        await Client.CreatePurchaseAsync(request, Ct);

        // Assert
        var product = await Client.GetFromJsonAsync<ProductResponse>(Route(ApiEndpoints.Products.Get, rice.Id), Ct);
        product!.StockMilli.Should().Be(200_000);
        product.LastPurchaseDate.Should().Be(Clock.Today);
        product.Cost.Should().NotBeNull();
        product.Cost!.AvgCostUsdMicros.Should().Be(620_000);
        product.Cost.AvgCostCupMicros.Should().Be(201_500_000);
        product.Cost.AverageRateCents.Should().Be(Rate325);
        product.Cost.SuggestedPriceCents.Should().Be(20_200);    // 201.50 → 202
        product.Cost.MarginBps.Should().Be(1_940);               // (250 − 201.50) / 250
    }

    [Fact]
    public async Task Create_ShouldSuggestThePrice_WhenItDiffersFromTheCurrentOne()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var request = await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 2, 6_200));

        var created = await Client.CreatePurchaseAsync(request, Ct);

        var suggestion = created.PriceSuggestions.Should().ContainSingle().Subject;
        suggestion.ProductId.Should().Be(rice.Id);
        suggestion.CurrentPriceCents.Should().Be(25_000);
        suggestion.SuggestedPriceCents.Should().Be(20_200);
    }

    [Fact]
    public async Task Create_ShouldNotSuggest_WhenThePriceAlreadyMatches()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct, salePriceCents: 20_200);
        var request = await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 2, 6_200));

        var created = await Client.CreatePurchaseAsync(request, Ct);

        created.PriceSuggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ShouldAverageTheCost_AcrossPurchases()
    {
        // 100 lb at 0.60 + 100 lb at 0.70 → 0.65
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 7_000)), Ct);

        var product = await Client.GetFromJsonAsync<ProductResponse>(Route(ApiEndpoints.Products.Get, rice.Id), Ct);

        product!.StockMilli.Should().Be(200_000);
        product.Cost!.AvgCostUsdMicros.Should().Be(650_000);
    }

    [Fact]
    public async Task Create_ShouldUseTheOverriddenFactor_ForThisBatchOnly()
    {
        // This batch came in 95 lb sacks.
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var line = new CreatePurchaseLineRequest
        {
            ProductId = rice.Id, QuantityMilli = 2_000, UnitCostCents = 5_700, ConversionFactorMilli = 95_000
        };

        var created = await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, line), Ct);

        created.Lines.Single().StockQuantityMilli.Should().Be(190_000);
        created.Lines.Single().LandedUnitCostUsdMicros.Should().Be(600_000);   // 114 USD / 190 lb
        var product = await Client.GetFromJsonAsync<ProductResponse>(Route(ApiEndpoints.Products.Get, rice.Id), Ct);
        product!.ConversionFactorMilli.Should().Be(100_000);
    }

    [Fact]
    public async Task Create_ShouldAddChargesToTheCost_PaidFromTheirOwnBox()
    {
        // 124 USD of rice + 1,300 CUP freight (= 4 USD) → 128 USD / 200 lb = 0.64
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var cupBox = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        var baseRequest = await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 2, 6_200));
        var request = WithCharges(baseRequest, new CreatePurchaseChargeRequest
        {
            Description = "Flete", Currency = CurrencyCode.CUP, AmountCents = 1_300_00, CashBoxId = cupBox.Id
        });

        var created = await Client.CreatePurchaseAsync(request, Ct);

        created.GoodsTotalCents.Should().Be(12_400);
        created.Charges.Should().ContainSingle().Which.CashBoxId.Should().Be(cupBox.Id);
        created.Lines.Single().AllocatedChargesUsdMicros.Should().Be(4_000_000);
        created.Lines.Single().LandedUnitCostUsdMicros.Should().Be(640_000);
    }

    [Fact]
    public async Task Create_ShouldReturn422_WhenTheBoxCurrencyDiffersFromThePurchase()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var cupBox = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        var usd = await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000));
        var request = new CreatePurchaseRequest
        {
            Date = usd.Date, SupplierId = usd.SupplierId, Currency = CurrencyCode.USD,
            RateCupPerUsdCents = usd.RateCupPerUsdCents, CashBoxId = cupBox.Id, Lines = usd.Lines
        };

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Purchases.Create, request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturn422_WhenTheChargeBoxCurrencyDiffersFromTheCharge()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var usdBox = await Client.GetCashBoxByNameAsync("Efectivo USD", Ct);
        var request = WithCharges(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)),
            new CreatePurchaseChargeRequest
            {
                Description = "Flete", Currency = CurrencyCode.CUP, AmountCents = 50_000, CashBoxId = usdBox.Id
            });

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Purchases.Create, request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturn422_WhenTheDateIsInTheFuture()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var request = await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today.AddDays(1), Ct,
            Sacks(rice.Id, 1, 6_000));

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Purchases.Create, request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturn422_WhenAFractionalQuantityIsBoughtInWholeUnits()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var line = new CreatePurchaseLineRequest { ProductId = rice.Id, QuantityMilli = 1_500, UnitCostCents = 6_000 };

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Purchases.Create,
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, line), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturn422_WhenAProductIsRepeated()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Purchases.Create,
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct,
                Sacks(rice.Id, 1, 6_000), Sacks(rice.Id, 2, 6_000)), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenThereAreNoLines()
    {
        var (_, supplier) = await Client.CreateRiceAsync(Ct);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Purchases.Create,
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenACostIsZero()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Purchases.Create,
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 0)), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- Void ----------

    [Fact]
    public async Task Void_ShouldRemoveTheStock_AndRestoreThePreviousCost()
    {
        // Arrange: 0.60 then 0.70 (average 0.65); voiding the second leaves exactly 0.60.
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);
        var second = await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 7_000)), Ct);

        // Act
        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.Purchases.Void, second.Id),
            new VoidPurchaseRequest { Reason = "Se cargó dos veces" }, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var voided = (await response.Content.ReadFromJsonAsync<PurchaseResponse>(Ct))!;
        voided.Status.Should().Be(PurchaseState.Voided);
        voided.VoidReason.Should().Be("Se cargó dos veces");
        voided.VoidedAt.Should().NotBeNull();

        var product = await Client.GetFromJsonAsync<ProductResponse>(Route(ApiEndpoints.Products.Get, rice.Id), Ct);
        product!.StockMilli.Should().Be(100_000);
        product.Cost!.AvgCostUsdMicros.Should().Be(600_000);
    }

    [Fact]
    public async Task Void_ShouldLeaveTheProductWithoutCost_WhenItWasItsOnlyPurchase()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var purchase = await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);

        await Client.PostAsJsonAsync(Route(ApiEndpoints.Purchases.Void, purchase.Id),
            new VoidPurchaseRequest { Reason = "Error" }, Ct);

        var product = await Client.GetFromJsonAsync<ProductResponse>(Route(ApiEndpoints.Products.Get, rice.Id), Ct);
        product!.StockMilli.Should().Be(0);
        product.Cost.Should().BeNull();
        product.LastPurchaseDate.Should().BeNull();
    }

    [Fact]
    public async Task Void_ShouldReturn422_WhenAlreadyVoided()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var purchase = await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);
        var route = Route(ApiEndpoints.Purchases.Void, purchase.Id);
        await Client.PostAsJsonAsync(route, new VoidPurchaseRequest { Reason = "Error" }, Ct);

        var response = await Client.PostAsJsonAsync(route, new VoidPurchaseRequest { Reason = "Otra vez" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Void_ShouldReturn400_WhenTheReasonIsBlank()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var purchase = await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.Purchases.Void, purchase.Id),
            new VoidPurchaseRequest { Reason = "  " }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Void_ShouldReturn404_WhenThePurchaseDoesNotExist()
    {
        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.Purchases.Void, Guid.NewGuid()),
            new VoidPurchaseRequest { Reason = "Error" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Queries ----------

    [Fact]
    public async Task GetAll_ShouldHideVoidedPurchases_UnlessAsked()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var request = await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000));
        await Client.CreatePurchaseAsync(request, Ct);
        var voided = await Client.CreatePurchaseAsync(request, Ct);
        await Client.PostAsJsonAsync(Route(ApiEndpoints.Purchases.Void, voided.Id),
            new VoidPurchaseRequest { Reason = "Error" }, Ct);

        var active = await Client.GetFromJsonAsync<PurchasesResponse>(ApiEndpoints.Purchases.GetAll, Ct);
        var all = await Client.GetFromJsonAsync<PurchasesResponse>(
            $"{ApiEndpoints.Purchases.GetAll}?includeVoided=true", Ct);

        active!.Total.Should().Be(1);
        active.Items.Single().ProductNames.Should().Be(rice.Name);
        all!.Total.Should().Be(2);
    }

    [Fact]
    public async Task GetAll_ShouldFilterByProduct()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var (beans, _) = await Client.CreateRiceAsync(Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(beans.Id, 1, 6_000)), Ct);

        var result = await Client.GetFromJsonAsync<PurchasesResponse>(
            $"{ApiEndpoints.Purchases.GetAll}?productId={beans.Id}", Ct);

        result!.Items.Should().ContainSingle().Which.ProductNames.Should().Be(beans.Name);
    }

    [Fact]
    public async Task ProductPurchases_ShouldListTheLinesOfTheProduct()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 2, 6_200)), Ct);

        var result = await Client.GetFromJsonAsync<ProductPurchasesResponse>(
            Route(ApiEndpoints.Products.Purchases, rice.Id), Ct);

        var only = result!.Items.Should().ContainSingle().Subject;
        only.SupplierName.Should().Be(supplier.Name);
        only.StockQuantityMilli.Should().Be(200_000);
        only.LandedUnitCostUsdMicros.Should().Be(620_000);
    }

    [Fact]
    public async Task SupplierProducts_ShouldShowTheLastCostOfEachProduct()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        Clock.Now = TestClock.Default.AddDays(-1);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);
        Clock.Now = TestClock.Default;
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 7_000)), Ct);

        var result = await Client.GetFromJsonAsync<SupplierProductsResponse>(
            Route(ApiEndpoints.Suppliers.Products, supplier.Id), Ct);

        var only = result!.Items.Should().ContainSingle().Subject;
        only.ProductId.Should().Be(rice.Id);
        only.PurchaseCount.Should().Be(2);
        only.LastPurchaseDate.Should().Be(Clock.Today);
        only.LastLandedUnitCostUsdMicros.Should().Be(700_000);
    }

    // ---------- Product rules that depend on purchases ----------

    [Fact]
    public async Task SetSalePrice_ShouldRecordAPurchaseChange_WhenAcceptingASuggestion()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var purchase = await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 2, 6_200)), Ct);

        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.SalePrice, rice.Id),
            new SetSalePriceRequest { SalePriceCents = 20_200, PurchaseId = purchase.Id }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        (await response.Content.ReadFromJsonAsync<ProductResponse>(Ct))!.SalePriceCents.Should().Be(20_200);
        var history = await Client.GetFromJsonAsync<PriceHistoryResponse>(
            Route(ApiEndpoints.Products.PriceHistory, rice.Id), Ct);
        var latest = history!.Items.First();
        latest.Reason.Should().Be(PriceChangeKind.Purchase);
        latest.PreviousPriceCents.Should().Be(25_000);
    }

    [Fact]
    public async Task UpdateProduct_ShouldReturn422_WhenChangingTheStockUnitAfterAPurchase()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);
        var update = ProductFaker.UpdateRequest(rice);
        var changed = new UpdateProductRequest
        {
            Code = update.Code, Name = update.Name, CategoryId = update.CategoryId,
            PurchaseUnit = update.PurchaseUnit, StockUnit = UnitCode.Kilogram,
            ConversionFactorMilli = 45_000, SalePriceCents = update.SalePriceCents, IsActive = true
        };

        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.Update, rice.Id), changed, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static CreatePurchaseRequest WithCharges(CreatePurchaseRequest request,
        params CreatePurchaseChargeRequest[] charges) => new()
    {
        Date = request.Date,
        SupplierId = request.SupplierId,
        Currency = request.Currency,
        RateCupPerUsdCents = request.RateCupPerUsdCents,
        LocationId = request.LocationId,
        CashBoxId = request.CashBoxId,
        Notes = request.Notes,
        Lines = request.Lines,
        Charges = charges
    };
}
