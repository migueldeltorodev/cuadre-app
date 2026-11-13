using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.CashBoxes;
using Cuadre.Api.Tests.Integration.Envelopes;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Api.Tests.Integration.Inventory;
using Cuadre.Api.Tests.Integration.Purchases;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;
using static Cuadre.Api.Tests.Integration.Closings.ClosingTestHelpers;
using static Cuadre.Api.Tests.Integration.Envelopes.EnvelopeTestHelpers;
using static Cuadre.Api.Tests.Integration.Inventory.InventoryTestHelpers;
using static Cuadre.Api.Tests.Integration.Purchases.PurchaseTestHelpers;

namespace Cuadre.Api.Tests.Integration.Expenses;

// Slice 7 (RF-GAS-01..04). Today is Thursday 2026-11-12.

[Collection(nameof(ApiCollection))]
public class ExpenseEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private Task<CashBoxResponse> CashCupAsync() => Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);

    private async Task<ExpenseCategoryResponse> CategoryAsync(string name) =>
        (await Client.GetFromJsonAsync<ExpenseCategoriesResponse>(ApiEndpoints.ExpenseCategories.GetAll, Ct))!
        .Items.Single(x => x.Name == name);

    /// <summary>Puts money in Gastos de operación of Efectivo CUP.</summary>
    private async Task<CashBoxResponse> FundOperationsAsync(long amountCents)
    {
        var cash = await CashCupAsync();
        var operations = (await Client.GetOverviewAsync(Ct)).Envelope(EnvelopeType.Operations);
        await Client.OpenAsync(Clock.Today.AddDays(-30), Ct, Target(cash.Id, operations.Id, amountCents));
        return cash;
    }

    private async Task<CreateExpenseRequest> ExpenseRequestAsync(long amountCents, DateOnly? date = null,
        Guid? envelopeId = null, string category = "Transporte") => new()
    {
        Date = date ?? Clock.Today,
        CategoryId = (await CategoryAsync(category)).Id,
        Description = "Flete del arroz",
        AmountCents = amountCents,
        CashBoxId = (await CashCupAsync()).Id,
        EnvelopeId = envelopeId
    };

    private async Task<ExpenseResponse> CreateExpenseAsync(CreateExpenseRequest request)
    {
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Expenses.Create, request, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<ExpenseResponse>(Ct))!;
    }

    private async Task<RecurringExpenseResponse> CreateRecurringAsync(ExpenseFrequency frequency, int dueDay,
        DateOnly startsOn, string name = "Gestor")
    {
        var response = await Client.PostAsJsonAsync(ApiEndpoints.RecurringExpenses.Create,
            new CreateRecurringExpenseRequest
            {
                Name = name,
                CategoryId = (await CategoryAsync("Gestor y contador")).Id,
                AmountCents = 300_000,
                CashBoxId = (await CashCupAsync()).Id,
                Frequency = frequency,
                DueDay = dueDay,
                StartsOn = startsOn
            }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<RecurringExpenseResponse>(Ct))!;
    }

    private async Task<IReadOnlyList<DueExpenseResponse>> DueAsync() =>
        (await Client.GetFromJsonAsync<DueExpensesResponse>(ApiEndpoints.RecurringExpenses.Due, Ct))!.Items.ToList();

    // ---------- Categories (RF-GAS-03) ----------

    [Fact]
    public async Task Categories_ShouldStartWithTheDefaults()
    {
        var categories = (await Client.GetFromJsonAsync<ExpenseCategoriesResponse>(
            ApiEndpoints.ExpenseCategories.GetAll, Ct))!.Items.ToList();

        categories.Should().HaveCount(7).And.OnlyContain(x => x.IsActive && x.ExpenseCount == 0);
        categories.Select(x => x.Name).Should().Contain(["Gestor y contador", "Transporte", "Otros"]);
    }

    [Fact]
    public async Task CreateCategory_ShouldReturn409_WhenTheNameExists_IgnoringCase()
    {
        var response = await Client.PostAsJsonAsync(ApiEndpoints.ExpenseCategories.Create,
            new CreateExpenseCategoryRequest { Name = "transporte" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateCategory_ShouldDeactivateIt_AndThenRefuseNewExpensesInIt()
    {
        var transport = await CategoryAsync("Transporte");
        var update = await Client.PutAsJsonAsync(Route(ApiEndpoints.ExpenseCategories.Update, transport.Id),
            new UpdateExpenseCategoryRequest { Name = "Transporte", IsActive = false }, Ct);
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Expenses.Create, await ExpenseRequestAsync(10_000), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateCategory_ShouldReturn422_WhenAnActiveRecurringExpenseUsesIt()
    {
        await CreateRecurringAsync(ExpenseFrequency.Monthly, 5, Clock.Today);
        var gestor = await CategoryAsync("Gestor y contador");

        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.ExpenseCategories.Update, gestor.Id),
            new UpdateExpenseCategoryRequest { Name = gestor.Name, IsActive = false }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ---------- Expenses (RF-GAS-02) ----------

    [Fact]
    public async Task Create_ShouldTakeTheMoneyOutOfOperacion_ByDefault()
    {
        var cash = await FundOperationsAsync(500_000);

        var expense = await CreateExpenseAsync(await ExpenseRequestAsync(120_000));

        expense.Number.Should().Be(1);
        expense.Status.Should().Be(ExpenseState.Confirmed);
        expense.EnvelopeName.Should().Be("Gastos de operación");
        expense.CategoryName.Should().Be("Transporte");
        expense.Currency.Should().Be(CurrencyCode.CUP);
        expense.Warning.Should().BeNull();

        var overview = await Client.GetOverviewAsync(Ct);
        overview.Balance(EnvelopeType.Operations, cash.Id).Should().Be(380_000);
        overview.BoxTotal(cash.Id).Should().Be(380_000);
    }

    [Fact]
    public async Task Create_ShouldBeAllowed_ButWarn_WhenTheEnvelopeGoesNegative()
    {
        var cash = await FundOperationsAsync(100_000);

        var expense = await CreateExpenseAsync(await ExpenseRequestAsync(150_000));

        expense.Warning.Should().NotBeNull();
        expense.Warning!.CashBoxId.Should().Be(cash.Id);
        expense.Warning.BalanceCents.Should().Be(-50_000);
    }

    [Fact]
    public async Task Create_ShouldReturn422_FromReposicion()
    {
        var replenishment = (await Client.GetOverviewAsync(Ct)).Envelope(EnvelopeType.Replenishment);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Expenses.Create,
            await ExpenseRequestAsync(10_000, envelopeId: replenishment.Id), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldAllowAnotherEnvelope_ThanOperacion()
    {
        var cash = await CashCupAsync();
        var owner = (await Client.GetOverviewAsync(Ct)).Envelope(EnvelopeType.OwnerPay);

        var expense = await CreateExpenseAsync(await ExpenseRequestAsync(10_000, envelopeId: owner.Id));

        expense.EnvelopeId.Should().Be(owner.Id);
        (await Client.GetOverviewAsync(Ct)).Balance(EnvelopeType.OwnerPay, cash.Id).Should().Be(-10_000);
    }

    [Fact]
    public async Task Create_ShouldReturn422_ForAFutureDate()
    {
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Expenses.Create,
            await ExpenseRequestAsync(10_000, Clock.Today.AddDays(1)), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WithoutADescription_OrWithAZeroAmount()
    {
        var request = await ExpenseRequestAsync(10_000);

        var noDescription = await Client.PostAsJsonAsync(ApiEndpoints.Expenses.Create,
            new CreateExpenseRequest
            {
                Date = request.Date,
                CategoryId = request.CategoryId,
                Description = "  ",
                AmountCents = 10_000,
                CashBoxId = request.CashBoxId
            }, Ct);
        var zero = await Client.PostAsJsonAsync(ApiEndpoints.Expenses.Create, await ExpenseRequestAsync(0), Ct);

        noDescription.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        zero.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ShouldReturn422_OnAClosedDay()
    {
        var yesterday = Clock.Today.AddDays(-1);
        var (rice, supplier) = await Client.CreateRiceAsync(Ct, 25_000);
        await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, yesterday, Ct, Sacks(rice.Id, 1, 4_000)), Ct);
        var (storage, counter) = await Client.GetSeededLocationsAsync(Ct);
        await Client.TransferAsync(yesterday, storage.Id, counter.Id, Ct, SacksOf(rice.Id, 1));
        await Client.CloseAsync(new CreateDailyClosingRequest
        {
            Date = yesterday,
            Counts = [Counted(rice.Id, counter.Id, 100)],
            Collections = []
        }, Ct);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Expenses.Create,
            await ExpenseRequestAsync(10_000, yesterday), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Movements_ShouldShowTheExpense_WithItsNumber()
    {
        await FundOperationsAsync(500_000);
        var expense = await CreateExpenseAsync(await ExpenseRequestAsync(120_000));

        var ledger = (await Client.GetFromJsonAsync<EnvelopeLedgerResponse>(
            Route(ApiEndpoints.Envelopes.Movements, expense.EnvelopeId), Ct))!;

        var row = ledger.Entries.Single(x => x.Movement.Kind == CashMovementType.Expense).Movement;
        row.AmountCents.Should().Be(-120_000);
        row.ExpenseId.Should().Be(expense.Id);
        row.ExpenseNumber.Should().Be(1);
        row.Description.Should().Be("Gasto #1: Flete del arroz");
    }

    [Fact]
    public async Task GetAll_ShouldFilter_AndAddUpTheConfirmedOnes()
    {
        await FundOperationsAsync(1_000_000);
        await CreateExpenseAsync(await ExpenseRequestAsync(100_000, Clock.Today.AddDays(-3)));
        await CreateExpenseAsync(await ExpenseRequestAsync(50_000));
        var other = await CreateExpenseAsync(await ExpenseRequestAsync(20_000, category: "Otros"));
        await Client.PostAsJsonAsync(Route(ApiEndpoints.Expenses.Void, other.Id),
            new VoidExpenseRequest { Reason = "Repetido" }, Ct);

        var all = (await Client.GetFromJsonAsync<ExpensesResponse>(ApiEndpoints.Expenses.GetAll, Ct))!;
        var withVoided = (await Client.GetFromJsonAsync<ExpensesResponse>(
            ApiEndpoints.Expenses.GetAll + "?includeVoided=true", Ct))!;
        var today = (await Client.GetFromJsonAsync<ExpensesResponse>(
            $"{ApiEndpoints.Expenses.GetAll}?from={Clock.Today:yyyy-MM-dd}&includeVoided=true", Ct))!;

        all.Total.Should().Be(2);
        all.Items.Select(x => x.AmountCents).Should().Equal(50_000, 100_000);   // newest first
        all.Totals.Should().ContainSingle().Which.AmountCents.Should().Be(150_000);
        withVoided.Total.Should().Be(3);
        withVoided.Totals.Single().AmountCents.Should().Be(150_000);           // voided never adds up
        today.Total.Should().Be(2);
        today.Totals.Single().AmountCents.Should().Be(50_000);
    }

    // ---------- Void (RF-GAS-04) ----------

    [Fact]
    public async Task Void_ShouldGiveTheMoneyBack_AndKeepTheExpense()
    {
        var cash = await FundOperationsAsync(500_000);
        var expense = await CreateExpenseAsync(await ExpenseRequestAsync(120_000, Clock.Today.AddDays(-2)));

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.Expenses.Void, expense.Id),
            new VoidExpenseRequest { Reason = "Lo pagó el proveedor" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var voided = (await response.Content.ReadFromJsonAsync<ExpenseResponse>(Ct))!;
        voided.Status.Should().Be(ExpenseState.Voided);
        voided.VoidReason.Should().Be("Lo pagó el proveedor");
        (await Client.GetOverviewAsync(Ct)).Balance(EnvelopeType.Operations, cash.Id).Should().Be(500_000);

        var ledger = (await Client.GetFromJsonAsync<EnvelopeLedgerResponse>(
            Route(ApiEndpoints.Envelopes.Movements, expense.EnvelopeId), Ct))!;
        var reversal = ledger.Entries.Single(x => x.Movement.Kind == CashMovementType.ExpenseVoid).Movement;
        reversal.Date.Should().Be(Clock.Today);
        reversal.AmountCents.Should().Be(120_000);
        reversal.IsVoided.Should().BeTrue();

        var again = await Client.PostAsJsonAsync(Route(ApiEndpoints.Expenses.Void, expense.Id),
            new VoidExpenseRequest { Reason = "Otra vez" }, Ct);
        again.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Void_ShouldReturn400_WithoutAReason()
    {
        var expense = await CreateExpenseAsync(await ExpenseRequestAsync(10_000));

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.Expenses.Void, expense.Id),
            new VoidExpenseRequest { Reason = "" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_ShouldReturn404_ForAnUnknownExpense()
    {
        var response = await Client.GetAsync(Route(ApiEndpoints.Expenses.Get, Guid.NewGuid()), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Recurring (RF-GAS-01) ----------

    [Fact]
    public async Task Due_ShouldProposeEveryMonthSinceItStarted_UpToToday()
    {
        // The 5th of each month since September: 5/9, 5/10 and 5/11 are due; 5/12 not yet.
        var gestor = await CreateRecurringAsync(ExpenseFrequency.Monthly, 5, new DateOnly(2026, 9, 1));

        var due = await DueAsync();

        gestor.EnvelopeName.Should().Be("Gastos de operación");
        due.Select(x => x.DueDate).Should().Equal(
            new DateOnly(2026, 9, 5), new DateOnly(2026, 10, 5), new DateOnly(2026, 11, 5));
        due.Select(x => x.PeriodStart).Should().Equal(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));
        due.Should().OnlyContain(x => x.Recurring.Id == gestor.Id);
    }

    [Fact]
    public async Task Confirm_ShouldCreateTheExpense_AndStopProposingThatPeriod()
    {
        var cash = await FundOperationsAsync(1_000_000);
        var gestor = await CreateRecurringAsync(ExpenseFrequency.Monthly, 5, new DateOnly(2026, 10, 1));

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.RecurringExpenses.Confirm, gestor.Id),
            new ConfirmRecurringExpenseRequest { PeriodStart = new DateOnly(2026, 10, 1), AmountCents = 350_000 }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var expense = (await response.Content.ReadFromJsonAsync<ExpenseResponse>(Ct))!;
        expense.Date.Should().Be(Clock.Today);
        expense.Description.Should().Be("Gestor");
        expense.AmountCents.Should().Be(350_000);
        expense.RecurringExpenseId.Should().Be(gestor.Id);
        expense.PeriodStart.Should().Be(new DateOnly(2026, 10, 1));
        (await Client.GetOverviewAsync(Ct)).Balance(EnvelopeType.Operations, cash.Id).Should().Be(650_000);

        (await DueAsync()).Select(x => x.PeriodStart).Should().Equal(new DateOnly(2026, 11, 1));

        var twice = await Client.PostAsJsonAsync(Route(ApiEndpoints.RecurringExpenses.Confirm, gestor.Id),
            new ConfirmRecurringExpenseRequest { PeriodStart = new DateOnly(2026, 10, 1) }, Ct);
        twice.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task VoidingAConfirmedOccurrence_ShouldProposeItAgain()
    {
        var gestor = await CreateRecurringAsync(ExpenseFrequency.Monthly, 5, new DateOnly(2026, 11, 1));
        var confirm = await Client.PostAsJsonAsync(Route(ApiEndpoints.RecurringExpenses.Confirm, gestor.Id),
            new ConfirmRecurringExpenseRequest { PeriodStart = new DateOnly(2026, 11, 1) }, Ct);
        var expense = (await confirm.Content.ReadFromJsonAsync<ExpenseResponse>(Ct))!;
        (await DueAsync()).Should().BeEmpty();

        await Client.PostAsJsonAsync(Route(ApiEndpoints.Expenses.Void, expense.Id),
            new VoidExpenseRequest { Reason = "Se cobró de más" }, Ct);

        (await DueAsync()).Select(x => x.PeriodStart).Should().Equal(new DateOnly(2026, 11, 1));
    }

    [Fact]
    public async Task Skip_ShouldStopProposingThatPeriod_WithoutMovingMoney()
    {
        var cash = await CashCupAsync();
        var gestor = await CreateRecurringAsync(ExpenseFrequency.Monthly, 5, new DateOnly(2026, 10, 1));

        var response = await Client.PostAsJsonAsync(Route(ApiEndpoints.RecurringExpenses.Skip, gestor.Id),
            new SkipRecurringExpenseRequest { PeriodStart = new DateOnly(2026, 10, 1) }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DueAsync()).Select(x => x.PeriodStart).Should().Equal(new DateOnly(2026, 11, 1));
        (await Client.GetOverviewAsync(Ct)).BoxTotal(cash.Id).Should().Be(0);
    }

    [Fact]
    public async Task ConfirmOrSkip_ShouldReturn422_ForAPeriodNotDueYet()
    {
        var gestor = await CreateRecurringAsync(ExpenseFrequency.Monthly, 20, new DateOnly(2026, 10, 1));

        // The 20th of November has not come yet.
        var confirm = await Client.PostAsJsonAsync(Route(ApiEndpoints.RecurringExpenses.Confirm, gestor.Id),
            new ConfirmRecurringExpenseRequest { PeriodStart = new DateOnly(2026, 11, 1) }, Ct);
        var skip = await Client.PostAsJsonAsync(Route(ApiEndpoints.RecurringExpenses.Skip, gestor.Id),
            new SkipRecurringExpenseRequest { PeriodStart = new DateOnly(2026, 12, 1) }, Ct);

        confirm.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        skip.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Due_ShouldProposeEveryWeek_OnItsWeekday()
    {
        // Mondays since Monday 2026-11-02: the 2nd and the 9th (the 16th has not come yet).
        await CreateRecurringAsync(ExpenseFrequency.Weekly, (int)Weekday.Monday, new DateOnly(2026, 11, 1),
            "Transporte semanal");

        var due = await DueAsync();

        due.Select(x => x.DueDate).Should().Equal(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 9));
    }

    [Fact]
    public async Task Due_ShouldIgnoreDeactivatedTemplates()
    {
        var gestor = await CreateRecurringAsync(ExpenseFrequency.Monthly, 5, new DateOnly(2026, 10, 1));

        var update = await Client.PutAsJsonAsync(Route(ApiEndpoints.RecurringExpenses.Update, gestor.Id),
            new UpdateRecurringExpenseRequest
            {
                Name = gestor.Name,
                CategoryId = gestor.CategoryId,
                AmountCents = gestor.AmountCents,
                CashBoxId = gestor.CashBoxId,
                EnvelopeId = gestor.EnvelopeId,
                Frequency = gestor.Frequency,
                DueDay = gestor.DueDay,
                StartsOn = gestor.StartsOn,
                IsActive = false
            }, Ct);

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DueAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRecurring_ShouldReturn400_ForADayOfTheMonthAbove28()
    {
        var response = await Client.PostAsJsonAsync(ApiEndpoints.RecurringExpenses.Create,
            new CreateRecurringExpenseRequest
            {
                Name = "Alquiler",
                CategoryId = (await CategoryAsync("Alquiler")).Id,
                AmountCents = 1_000_000,
                CashBoxId = (await CashCupAsync()).Id,
                Frequency = ExpenseFrequency.Monthly,
                DueDay = 31,
                StartsOn = Clock.Today
            }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateRecurring_ShouldReturn409_ForARepeatedName()
    {
        await CreateRecurringAsync(ExpenseFrequency.Monthly, 5, Clock.Today);

        var response = await Client.PostAsJsonAsync(ApiEndpoints.RecurringExpenses.Create,
            new CreateRecurringExpenseRequest
            {
                Name = "GESTOR",
                CategoryId = (await CategoryAsync("Gestor y contador")).Id,
                AmountCents = 1_000,
                CashBoxId = (await CashCupAsync()).Id,
                Frequency = ExpenseFrequency.Monthly,
                DueDay = 1,
                StartsOn = Clock.Today
            }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
