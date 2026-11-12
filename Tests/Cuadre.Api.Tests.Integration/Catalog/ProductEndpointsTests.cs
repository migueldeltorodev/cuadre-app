using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Fakers;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Catalog;

[Collection(nameof(ApiCollection))]
public class ProductEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    // ---------- Create ----------

    [Fact]
    public async Task Create_ShouldGenerateCodeFromCategoryPrefix_WhenCodeIsBlank()
    {
        // Arrange
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);

        // Act
        var first = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);
        var second = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id, code: "  "), Ct);

        // Assert
        first.Code.Should().Be("ARR-001");
        second.Code.Should().Be("ARR-002");
    }

    [Fact]
    public async Task Create_ShouldReturnCreated_WithAllFieldsAndDerivedDecimals()
    {
        // Arrange
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var imported = await Client.CreateCategoryAsync("Importado", Ct, parentId: rice.Id);
        var supplier = await Client.CreateSupplierAsync(Ct);
        var request = ProductFaker.CreateRequest(rice.Id, imported.Id, supplier.Id, code: "arr-esp",
            barcode: "7501234567890");

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Products.Create, request, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ProductResponse>(Ct);
        created!.Code.Should().Be("ARR-ESP");
        created.SubcategoryId.Should().Be(imported.Id);
        created.UsualSupplierId.Should().Be(supplier.Id);
        created.PurchaseUnit.Should().Be(UnitCode.Sack);
        created.StockUnit.Should().Be(UnitCode.Pound);
        created.ConversionFactorMilli.Should().Be(100_000);
        created.AllowsDecimals.Should().BeTrue();
        created.SalePriceCents.Should().Be(25_000);
        created.IsPriceChangedSinceListed.Should().BeTrue();   // never copied to the sign yet
        created.IsActive.Should().BeTrue();
        response.Headers.Location!.ToString().Should().EndWith($"/api/products/{created.Id}");
    }

    [Fact]
    public async Task Create_ShouldRecordTheFirstPrice_InTheHistory()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var product = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);

        var history = await Client.GetFromJsonAsync<PriceHistoryResponse>(
            Route(ApiEndpoints.Products.PriceHistory, product.Id), Ct);

        var only = history!.Items.Should().ContainSingle().Subject;
        only.PriceCents.Should().Be(25_000);
        only.PreviousPriceCents.Should().BeNull();
        only.Reason.Should().Be(PriceChangeKind.Manual);
        only.EffectiveDate.Should().Be(Clock.Today);
    }

    [Fact]
    public async Task Create_ShouldNotRecordHistory_WhenProductHasNoPrice()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var product = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id, salePriceCents: null), Ct);

        var history = await Client.GetFromJsonAsync<PriceHistoryResponse>(
            Route(ApiEndpoints.Products.PriceHistory, product.Id), Ct);

        history!.Items.Should().BeEmpty();
        product.IsPriceChangedSinceListed.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenCodeExistsIgnoringCase()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);   // ARR-001

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Products.Create,
            ProductFaker.CreateRequest(rice.Id, code: "arr-001"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenBarcodeExists()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id, barcode: "123456"), Ct);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Products.Create,
            ProductFaker.CreateRequest(rice.Id, barcode: "123456"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_ShouldReturnUnprocessable_WhenSubcategoryBelongsToAnotherCategory()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var beans = await Client.CreateCategoryAsync("Frijoles", Ct);
        var blackBeans = await Client.CreateCategoryAsync("Negros", Ct, parentId: beans.Id);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Products.Create,
            ProductFaker.CreateRequest(rice.Id, blackBeans.Id), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturnUnprocessable_WhenCategoryIsASubcategory()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var imported = await Client.CreateCategoryAsync("Importado", Ct, parentId: rice.Id);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Products.Create,
            ProductFaker.CreateRequest(imported.Id, code: "IMP-001"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturnUnprocessable_WhenSupplierIsInactive()
    {
        // Arrange
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var supplier = await Client.CreateSupplierAsync(Ct);
        await Client.PutAsJsonAsync(Route(ApiEndpoints.Suppliers.Update, supplier.Id),
            new UpdateSupplierRequest { Name = supplier.Name, IsActive = false }, Ct);

        // Act
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Products.Create,
            ProductFaker.CreateRequest(rice.Id, supplierId: supplier.Id), Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenSameUnitHasFactorOtherThanOne()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var request = ProductFaker.CreateRequest(rice.Id, purchaseUnit: UnitCode.Each, stockUnit: UnitCode.Each,
            conversionFactorMilli: 12_000, minStockMilli: 0);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Products.Create, request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ValidationFailureResponse>(Ct);
        body!.Errors.Should().Contain(e => e.PropertyName == "ConversionFactorMilli");
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenCountableUnitGetsFractions()
    {
        // A box of 24 units: the minimum stock cannot be 2.5 units.
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var request = ProductFaker.CreateRequest(rice.Id, purchaseUnit: UnitCode.Box, stockUnit: UnitCode.Each,
            conversionFactorMilli: 24_000, minStockMilli: 2_500);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Products.Create, request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ValidationFailureResponse>(Ct);
        body!.Errors.Should().ContainSingle(e => e.PropertyName == "MinStockMilli");
    }

    // ---------- Next code ----------

    [Fact]
    public async Task NextCode_ShouldReturnTheCodeTheNextProductWouldGet()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);

        var next = await Client.GetFromJsonAsync<NextProductCodeResponse>(
            $"{ApiEndpoints.Products.NextCode}?categoryId={rice.Id}", Ct);

        next!.Code.Should().Be("ARR-002");
    }

    [Fact]
    public async Task NextCode_ShouldReturnNotFound_WhenCategoryDoesNotExist()
    {
        var response = await Client.GetAsync($"{ApiEndpoints.Products.NextCode}?categoryId={Guid.NewGuid()}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Update ----------

    [Fact]
    public async Task Update_ShouldAppendHistory_WhenPriceChanges()
    {
        // Arrange
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var product = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);
        Clock.Now = Clock.Now.AddDays(1);

        // Act
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.Update, product.Id),
            ProductFaker.UpdateRequest(product, salePriceCents: 27_500), Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await Client.GetFromJsonAsync<PriceHistoryResponse>(
            Route(ApiEndpoints.Products.PriceHistory, product.Id), Ct);
        history!.Items.Select(h => h.PriceCents).Should().Equal(27_500, 25_000);   // newest first
        history.Items.First().PreviousPriceCents.Should().Be(25_000);
    }

    [Fact]
    public async Task Update_ShouldNotAppendHistory_WhenPriceIsTheSame()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var product = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);
        var request = ProductFaker.UpdateRequest(product);

        (await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.Update, product.Id), request, Ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await Client.GetFromJsonAsync<PriceHistoryResponse>(
            Route(ApiEndpoints.Products.PriceHistory, product.Id), Ct);
        history!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_ShouldReturnUnprocessable_WhenRemovingAnExistingPrice()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var product = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);

        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.Update, product.Id),
            ProductFaker.UpdateRequest(product, keepPrice: false), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Update_ShouldKeepAnInactiveSupplier_ThatWasAlreadyChosen()
    {
        // Arrange — the supplier is deactivated AFTER being set on the product
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var supplier = await Client.CreateSupplierAsync(Ct);
        var product = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id, supplierId: supplier.Id), Ct);
        await Client.PutAsJsonAsync(Route(ApiEndpoints.Suppliers.Update, supplier.Id),
            new UpdateSupplierRequest { Name = supplier.Name, IsActive = false }, Ct);

        // Act — an unrelated edit must not be blocked
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.Update, product.Id),
            ProductFaker.UpdateRequest(product, salePriceCents: 30_000), Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ShouldDeactivate_AndHideFromDefaultList()
    {
        // Arrange
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var product = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);

        // Act
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.Update, product.Id),
            ProductFaker.UpdateRequest(product, isActive: false), Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var active = await Client.GetFromJsonAsync<ProductsResponse>(ApiEndpoints.Products.GetAll, Ct);
        active!.Items.Should().BeEmpty();
        var all = await Client.GetFromJsonAsync<ProductsResponse>(
            $"{ApiEndpoints.Products.GetAll}?includeInactive=true", Ct);
        all!.Items.Should().ContainSingle(p => p.Id == product.Id && !p.IsActive);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var product = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);

        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.Update, Guid.NewGuid()),
            ProductFaker.UpdateRequest(product), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Get all ----------

    [Fact]
    public async Task GetAll_ShouldFilterBySearchAndCategory_AndPage()
    {
        // Arrange
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        var imported = await Client.CreateCategoryAsync("Importado", Ct, parentId: rice.Id);
        var drinks = await Client.CreateCategoryAsync("Bebidas", Ct);
        var guyanese = await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id, imported.Id), Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(drinks.Id), Ct);

        // Act
        var bySubcategory = await Client.GetFromJsonAsync<ProductsResponse>(
            $"{ApiEndpoints.Products.GetAll}?categoryId={imported.Id}", Ct);
        var byCategory = await Client.GetFromJsonAsync<ProductsResponse>(
            $"{ApiEndpoints.Products.GetAll}?categoryId={rice.Id}", Ct);
        var byCode = await Client.GetFromJsonAsync<ProductsResponse>(
            $"{ApiEndpoints.Products.GetAll}?search=beb-0", Ct);
        var paged = await Client.GetFromJsonAsync<ProductsResponse>(
            $"{ApiEndpoints.Products.GetAll}?page=1&pageSize=2", Ct);

        // Assert
        bySubcategory!.Items.Should().ContainSingle(p => p.Id == guyanese.Id);
        byCategory!.Total.Should().Be(2);
        byCode!.Items.Should().ContainSingle(p => p.Code == "BEB-001");
        paged!.Items.Should().HaveCount(2);
        paged.Total.Should().Be(3);
        paged.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_ShouldTreatLikeWildcardsLiterally()
    {
        var rice = await Client.CreateCategoryAsync("Arroz", Ct);
        await Client.CreateProductAsync(ProductFaker.CreateRequest(rice.Id), Ct);

        var response = await Client.GetFromJsonAsync<ProductsResponse>(
            $"{ApiEndpoints.Products.GetAll}?search=%25", Ct);   // "%"

        response!.Items.Should().BeEmpty();
    }

    // ---------- Units ----------

    [Fact]
    public async Task Units_ShouldListEveryUnit_WithItsDecimalRule()
    {
        var units = await Client.GetFromJsonAsync<UnitsResponse>(ApiEndpoints.Units.GetAll, Ct);

        units!.Items.Should().HaveCount(Enum.GetValues<UnitCode>().Length);
        units.Items.Single(u => u.Code == UnitCode.Pound).AllowsDecimals.Should().BeTrue();
        units.Items.Single(u => u.Code == UnitCode.Each).AllowsDecimals.Should().BeFalse();
    }
}
