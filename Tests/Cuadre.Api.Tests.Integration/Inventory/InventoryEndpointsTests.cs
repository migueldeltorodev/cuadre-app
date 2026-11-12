using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Api.Tests.Integration.Purchases;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;
using static Cuadre.Api.Tests.Integration.Inventory.InventoryTestHelpers;
using static Cuadre.Api.Tests.Integration.Purchases.PurchaseTestHelpers;

namespace Cuadre.Api.Tests.Integration.Inventory;

// The rice of the purchase tests: 100 lb sacks, sold by the lb, minimum 20 lb.
// Bought at 62 USD the sack at 325 → 0.62 USD/lb, 201.50 CUP/lb.

[Collection(nameof(ApiCollection))]
public class InventoryEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<ProductResponse> BuyRiceAsync(long sacks, long usdCentsPerSack = 6_200)
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, sacks, usdCentsPerSack)),
            Ct);
        return rice;
    }

    private async Task<ProductResponse> GetProductAsync(Guid id) =>
        (await Client.GetFromJsonAsync<ProductResponse>(Route(ApiEndpoints.Products.Get, id), Ct))!;

    // ---------- Transfers ----------

    [Fact]
    public async Task Transfer_ShouldMoveStockBetweenLocations_ConvertingPurchaseUnits()
    {
        // Arrange: 2 sacks = 200 lb in the Almacén
        var rice = await BuyRiceAsync(2);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);

        // Act: open one sack at the counter
        var operation = await Client.TransferAsync(Clock.Today, storage.Id, counter.Id, Ct, SacksOf(rice.Id, 1));

        // Assert
        operation.Kind.Should().Be(StockOperationType.Transfer);
        operation.Number.Should().Be(1);
        operation.LocationName.Should().Be(storage.Name);
        operation.ToLocationName.Should().Be(counter.Name);
        operation.Warnings.Should().BeEmpty();
        var line = operation.Lines.Should().ContainSingle().Subject;
        line.EnteredQuantityMilli.Should().Be(1_000);
        line.EnteredInPurchaseUnits.Should().BeTrue();
        line.QuantityMilli.Should().Be(100_000);

        var stock = await Client.GetStockAsync(Ct);
        stock.QuantityAt(rice.Id, storage.Id).Should().Be(100_000);
        stock.QuantityAt(rice.Id, counter.Id).Should().Be(100_000);
        stock.Items.Single(x => x.ProductId == rice.Id).TotalMilli.Should().Be(200_000);
        (await GetProductAsync(rice.Id)).Cost!.AvgCostUsdMicros.Should().Be(620_000);   // moving costs nothing
    }

    [Fact]
    public async Task Transfer_ShouldBeAllowed_ButWarn_WhenTheOriginGoesNegative()
    {
        var rice = await BuyRiceAsync(1);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);

        var operation = await Client.TransferAsync(Clock.Today, storage.Id, counter.Id, Ct, Pounds(rice.Id, 150));

        var warning = operation.Warnings.Should().ContainSingle().Subject;
        warning.ProductId.Should().Be(rice.Id);
        warning.LocationId.Should().Be(storage.Id);
        warning.BalanceMilli.Should().Be(-50_000);   // 100 − 150
        warning.StockUnit.Should().Be(UnitCode.Pound);
    }

    [Fact]
    public async Task Transfer_ShouldReturn422_WhenHalfASackIsTypedInSacks()
    {
        var rice = await BuyRiceAsync(2);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.StockOperations.CreateTransfer,
            new CreateTransferRequest
            {
                Date = Clock.Today,
                FromLocationId = storage.Id,
                ToLocationId = counter.Id,
                Lines = [new StockOperationLineRequest { ProductId = rice.Id, QuantityMilli = 1_500, InPurchaseUnits = true }]
            }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Transfer_ShouldReturn400_WhenOriginAndDestinationAreTheSame()
    {
        var rice = await BuyRiceAsync(1);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.StockOperations.CreateTransfer,
            new CreateTransferRequest
            {
                Date = Clock.Today,
                FromLocationId = storage.Id,
                ToLocationId = storage.Id,
                Lines = [Pounds(rice.Id, 10)]
            }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Transfer_ShouldReturn422_WhenTheDateIsInTheFuture()
    {
        var rice = await BuyRiceAsync(1);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.StockOperations.CreateTransfer,
            new CreateTransferRequest
            {
                Date = Clock.Today.AddDays(1),
                FromLocationId = storage.Id,
                ToLocationId = counter.Id,
                Lines = [Pounds(rice.Id, 10)]
            }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ---------- Exits ----------

    [Fact]
    public async Task Exit_ShouldLowerTheStock_AndValueItAtTheAverageCost()
    {
        var rice = await BuyRiceAsync(2);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);

        var operation = await Client.ExitAsync(Clock.Today, storage.Id, ExitReasonCode.Damaged, Ct,
            Pounds(rice.Id, 10));

        operation.ExitReason.Should().Be(ExitReasonCode.Damaged);
        var line = operation.Lines.Should().ContainSingle().Subject;
        line.QuantityMilli.Should().Be(10_000);
        line.UnitCostCupMicros.Should().Be(201_500_000);
        line.ValueCupCents.Should().Be(201_500);   // 10 lb × 201.50 = 2,015.00 CUP

        var product = await GetProductAsync(rice.Id);
        product.StockMilli.Should().Be(190_000);
        product.Cost!.AvgCostUsdMicros.Should().Be(620_000);   // an outflow never moves the average
    }

    [Fact]
    public async Task Exit_ShouldReturn400_WhenTheQuantityIsZero()
    {
        var rice = await BuyRiceAsync(1);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.StockOperations.CreateExit, new CreateExitRequest
        {
            Date = Clock.Today,
            LocationId = storage.Id,
            Reason = ExitReasonCode.Shrinkage,
            Lines = [Pounds(rice.Id, 0)]
        }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- Counts ----------

    [Fact]
    public async Task Count_ShouldAdjustAShortage_AsALossAtCost()
    {
        var rice = await BuyRiceAsync(2);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);

        var operation = await Client.CountAsync(Clock.Today, storage.Id, Ct, Pounds(rice.Id, 195));

        var line = operation.Lines.Should().ContainSingle().Subject;
        line.ExpectedMilli.Should().Be(200_000);
        line.CountedMilli.Should().Be(195_000);
        line.QuantityMilli.Should().Be(-5_000);
        line.ValueCupCents.Should().Be(-100_750);   // −5 lb × 201.50

        (await GetProductAsync(rice.Id)).StockMilli.Should().Be(195_000);

        var history = await Client.GetFromJsonAsync<StockOperationsResponse>(
            ApiEndpoints.StockOperations.GetAll + "?kind=Count", Ct);
        history!.Items.Should().ContainSingle().Which.ValueCupCents.Should().Be(100_750);   // positive = loss
    }

    [Fact]
    public async Task Count_ShouldAddASurplus_WithoutMovingTheAverage()
    {
        var rice = await BuyRiceAsync(2);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);

        var operation = await Client.CountAsync(Clock.Today, storage.Id, Ct, Pounds(rice.Id, 210));

        operation.Lines.Single().QuantityMilli.Should().Be(10_000);
        var product = await GetProductAsync(rice.Id);
        product.StockMilli.Should().Be(210_000);
        product.Cost!.AvgCostUsdMicros.Should().Be(620_000);
    }

    [Fact]
    public async Task Count_ShouldOnlyExpectWhatWasThere_OnTheCountDate()
    {
        // 1 sack two days ago, another today: a count dated yesterday expects only the first.
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today.AddDays(-2), Ct, Sacks(rice.Id, 1, 6_000)), Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);

        var operation = await Client.CountAsync(Clock.Today.AddDays(-1), storage.Id, Ct, Pounds(rice.Id, 100));

        operation.Lines.Single().ExpectedMilli.Should().Be(100_000);
        operation.Lines.Single().QuantityMilli.Should().Be(0);
        (await GetProductAsync(rice.Id)).StockMilli.Should().Be(200_000);   // nothing to adjust
    }

    [Fact]
    public async Task Count_ShouldSetTheLastCountDate_OfTheLocation()
    {
        var rice = await BuyRiceAsync(1);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);

        await Client.CountAsync(Clock.Today, storage.Id, Ct, Pounds(rice.Id, 100));

        var stock = await Client.GetStockAsync(Ct);
        stock.Locations.Select(x => x.Id).Should().StartWith(counter.Id);   // Mostrador first
        stock.Locations.Single(x => x.Id == storage.Id).LastCountDate.Should().Be(Clock.Today);
        stock.Locations.Single(x => x.Id == counter.Id).LastCountDate.Should().BeNull();
    }

    // ---------- Void ----------

    [Fact]
    public async Task Void_ShouldReverseTheTransfer_AndRefuseASecondVoid()
    {
        var rice = await BuyRiceAsync(2);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);
        var operation = await Client.TransferAsync(Clock.Today, storage.Id, counter.Id, Ct, Pounds(rice.Id, 50));
        var route = Route(ApiEndpoints.StockOperations.Void, operation.Id);

        var response = await Client.PostAsJsonAsync(route, new VoidStockOperationRequest { Reason = "Error" }, Ct);
        var again = await Client.PostAsJsonAsync(route, new VoidStockOperationRequest { Reason = "Error" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var voided = (await response.Content.ReadFromJsonAsync<StockOperationResponse>(Ct))!;
        voided.Status.Should().Be(StockOperationState.Voided);
        voided.VoidReason.Should().Be("Error");
        again.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var stock = await Client.GetStockAsync(Ct);
        stock.QuantityAt(rice.Id, storage.Id).Should().Be(200_000);
        stock.QuantityAt(rice.Id, counter.Id).Should().Be(0);
    }

    [Fact]
    public async Task Void_ShouldReplayTheCost_AsIfTheExitNeverHappened()
    {
        // 100 lb at 0.60; an exit empties it; 100 lb at 0.90 restarts the average at 0.90.
        // Voiding the exit: 100 at 0.60 + 100 at 0.90 → 0.75.
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 6_000)), Ct);
        var exit = await Client.ExitAsync(Clock.Today, storage.Id, ExitReasonCode.Damaged, Ct, Pounds(rice.Id, 100));
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 9_000)), Ct);
        (await GetProductAsync(rice.Id)).Cost!.AvgCostUsdMicros.Should().Be(900_000);

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.StockOperations.Void, exit.Id),
            new VoidStockOperationRequest { Reason = "No se rompió" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await GetProductAsync(rice.Id);
        product.StockMilli.Should().Be(200_000);
        product.Cost!.AvgCostUsdMicros.Should().Be(750_000);
    }

    [Fact]
    public async Task Void_ShouldReturn404_WhenTheOperationDoesNotExist()
    {
        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.StockOperations.Void, Guid.NewGuid()),
            new VoidStockOperationRequest { Reason = "x" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Kardex and movements ----------

    [Fact]
    public async Task Kardex_ShouldShowRunningBalances_PerLocationAndTotal()
    {
        var rice = await BuyRiceAsync(2);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);
        await Client.TransferAsync(Clock.Today, storage.Id, counter.Id, Ct, Pounds(rice.Id, 50));

        var kardex = await Client.GetFromJsonAsync<KardexResponse>(Route(ApiEndpoints.Inventory.Kardex, rice.Id), Ct);

        kardex!.Entries.Select(x => x.Movement.Kind).Should().Equal(
            StockMovementType.Purchase, StockMovementType.TransferOut, StockMovementType.TransferIn);
        kardex.Entries.Select(x => x.LocationBalanceMilli).Should().Equal(200_000L, 150_000L, 50_000L);
        kardex.Entries.Select(x => x.TotalBalanceMilli).Should().Equal(200_000L, 150_000L, 200_000L);
        kardex.Entries.First().Movement.PurchaseNumber.Should().Be(1);
        kardex.ClosingBalances.Should().HaveCount(2);
    }

    [Fact]
    public async Task Kardex_ShouldReturn404_WhenTheProductDoesNotExist()
    {
        var response = await Client.GetAsync(Route(ApiEndpoints.Inventory.Kardex, Guid.NewGuid()), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Movements_ShouldListTodaysMovements_WithTheirDocuments()
    {
        var rice = await BuyRiceAsync(1);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);
        var transfer = await Client.TransferAsync(Clock.Today, storage.Id, counter.Id, Ct, Pounds(rice.Id, 10));

        var movements = await Client.GetFromJsonAsync<StockMovementsResponse>(ApiEndpoints.Inventory.Movements, Ct);

        movements!.Date.Should().Be(Clock.Today);
        movements.Items.Should().HaveCount(3);
        movements.Items.Where(x => x.OperationId == transfer.Id).Should().HaveCount(2)
            .And.OnlyContain(x => x.OperationNumber == transfer.Number);
    }

    // ---------- Stock table and shopping list ----------

    [Fact]
    public async Task Stock_ShouldFlagLowStock_AndFilterByIt()
    {
        // 100 lb − 85 lb = 15 lb, under the 20 lb minimum
        var rice = await BuyRiceAsync(1, usdCentsPerSack: 6_000);
        var other = await BuyRiceAsync(1);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);
        await Client.ExitAsync(Clock.Today, storage.Id, ExitReasonCode.Expired, Ct, Pounds(rice.Id, 85));

        var low = await Client.GetStockAsync(Ct, "?lowOnly=true");

        var row = low.Items.Should().ContainSingle().Subject;
        row.ProductId.Should().Be(rice.Id);
        row.IsLow.Should().BeTrue();
        row.TotalMilli.Should().Be(15_000);
        (await Client.GetStockAsync(Ct)).Items.Single(x => x.ProductId == other.Id).IsLow.Should().BeFalse();
    }

    [Fact]
    public async Task ShoppingList_ShouldSuggestWholeSacks_ForLowProducts()
    {
        // Missing 5 lb → 1 sack → 100 lb × 0.60 USD = 60 USD
        var rice = await BuyRiceAsync(1, usdCentsPerSack: 6_000);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);
        await Client.ExitAsync(Clock.Today, storage.Id, ExitReasonCode.Expired, Ct, Pounds(rice.Id, 85));

        var list = await Client.GetFromJsonAsync<ShoppingListResponse>(ApiEndpoints.Inventory.ShoppingList, Ct);

        var item = list!.Items.Should().ContainSingle().Subject;
        item.ProductId.Should().Be(rice.Id);
        item.MissingMilli.Should().Be(5_000);
        item.SuggestedPurchaseQuantityMilli.Should().Be(1_000);
        item.LastUnitCostUsdMicros.Should().Be(600_000);
        item.EstimatedUsdCents.Should().Be(6_000);
    }

    // ---------- Locations ----------

    [Fact]
    public async Task CreateLocation_ShouldReturnCreated_AndRefuseADuplicateName()
    {
        var request = new CreateLocationRequest { Name = "Almacén 2", Kind = LocationType.Storage };

        var created = await Client.PostAsJsonAsync(ApiEndpoints.Locations.Create, request, Ct);
        var duplicate = await Client.PostAsJsonAsync(ApiEndpoints.Locations.Create,
            new CreateLocationRequest { Name = "almacén 2", Kind = LocationType.Counter }, Ct);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var location = (await created.Content.ReadFromJsonAsync<LocationResponse>(Ct))!;
        location.Kind.Should().Be(LocationType.Storage);
        location.IsActive.Should().BeTrue();
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateLocation_ShouldRefuseToDeactivate_WhileItHoldsStock()
    {
        // Seeded locations first: once "Almacén 2" exists there are two active storages.
        await BuyRiceAsync(1);
        var (storage, _) = await Client.GetSeededLocationsAsync(Ct);
        var second = await CreateLocationAsync("Almacén 2", LocationType.Storage);

        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Locations.Update, storage.Id),
            new UpdateLocationRequest { Name = storage.Name, IsActive = false }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        second.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateLocation_ShouldRefuseToDeactivate_TheOnlyCounter()
    {
        var (_, counter) = await Client.GetSeededLocationsAsync(Ct);

        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Locations.Update, counter.Id),
            new UpdateLocationRequest { Name = counter.Name, IsActive = false }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateLocation_ShouldRenameAndDeactivate_AnEmptyLocation()
    {
        var second = await CreateLocationAsync("Mostrador 2", LocationType.Counter);

        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Locations.Update, second.Id),
            new UpdateLocationRequest { Name = "Mostrador viejo", IsActive = false }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var updated = (await response.Content.ReadFromJsonAsync<LocationResponse>(Ct))!;
        updated.Name.Should().Be("Mostrador viejo");
        updated.IsActive.Should().BeFalse();
    }

    private async Task<LocationResponse> CreateLocationAsync(string name, LocationType kind)
    {
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Locations.Create,
            new CreateLocationRequest { Name = name, Kind = kind }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<LocationResponse>(Ct))!;
    }
}
