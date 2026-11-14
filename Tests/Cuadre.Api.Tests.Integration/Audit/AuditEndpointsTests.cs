using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Envelopes;
using Cuadre.Api.Tests.Integration.CashBoxes;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Api.Tests.Integration.Purchases;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;
using static Cuadre.Api.Tests.Integration.Purchases.PurchaseTestHelpers;

namespace Cuadre.Api.Tests.Integration.Audit;

// RF-DAT-08. The clock says 2026-11-12 15:00 UTC (the shop's time zone in the tests is UTC too).

[Collection(nameof(ApiCollection))]
public class AuditEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<PagedResponse<AuditEventResponse>> GetLogAsync(string query = "")
    {
        var response = await Client.GetAsync($"{ApiEndpoints.Audit.GetAll}{query}", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<PagedResponse<AuditEventResponse>>(Ct))!;
    }

    private async Task<HttpResponseMessage> SaveSettingsAsync(Func<BusinessSettingsResponse, UpdateBusinessSettingsRequest> change)
    {
        var current = (await Client.GetFromJsonAsync<BusinessSettingsResponse>(ApiEndpoints.Settings.Get, Ct))!;
        return await Client.PutAsJsonAsync(ApiEndpoints.Settings.Update, change(current), Ct);
    }

    private static UpdateBusinessSettingsRequest Same(BusinessSettingsResponse s, int? markupBps = null,
        string? address = null) => new()
    {
        Name = s.Name,
        Address = address ?? s.Address,
        ClosedDays = s.ClosedDays,
        CashDifferenceThresholdCents = s.CashDifferenceThresholdCents,
        ExchangeBufferCents = s.ExchangeBufferCents,
        DefaultMarkupBps = markupBps ?? s.DefaultMarkupBps,
        PriceRoundingStepCents = s.PriceRoundingStepCents
    };

    [Fact]
    public async Task Log_ShouldBeEmpty_OnAFreshInstall()
    {
        // The seeded plan, pay scheme and the products' first prices are where things start, not changes.
        var log = await GetLogAsync();

        log.Items.Should().BeEmpty();
        log.Total.Should().Be(0);
    }

    // ---------- Configuración ----------

    [Fact]
    public async Task Settings_ShouldLogOnlyTheFieldsThatChanged()
    {
        var before = (await Client.GetFromJsonAsync<BusinessSettingsResponse>(ApiEndpoints.Settings.Get, Ct))!;

        (await SaveSettingsAsync(s => Same(s, markupBps: before.DefaultMarkupBps + 500))).EnsureSuccessStatusCode();

        var entry = (await GetLogAsync()).Items.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(AuditEventKindCode.ConfigChanged);
        entry.Subject.Should().Be(AuditSubjectCode.Settings);
        entry.At.Should().Be(Clock.Now.UtcDateTime);
        entry.Changes.Should().ContainSingle().Which.Field.Should().Be("Recargo por defecto");
        entry.Changes[0].After.Should().EndWith("%");
    }

    [Fact]
    public async Task Settings_ShouldLogNothing_WhenSavedWithoutChanges()
    {
        (await SaveSettingsAsync(s => Same(s))).EnsureSuccessStatusCode();

        (await GetLogAsync()).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CashBox_ShouldLogTheRename_WithTheOldAndNewName()
    {
        var box = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);

        (await Client.PutAsJsonAsync(Route(ApiEndpoints.CashBoxes.Update, box.Id),
            new UpdateCashBoxRequest { Name = "Caja del mostrador", IsActive = true }, Ct)).EnsureSuccessStatusCode();

        var entry = (await GetLogAsync()).Items.Should().ContainSingle().Subject;
        entry.Title.Should().Be("Caja «Caja del mostrador»");
        entry.SubjectId.Should().Be(box.Id);
        entry.Changes.Should().BeEquivalentTo([
            new FieldChangeResponse { Field = "Nombre", Before = "Efectivo CUP", After = "Caja del mostrador" }
        ]);
    }

    [Fact]
    public async Task ExchangeRate_ShouldLogACorrection_NotTheDailyLoad()
    {
        var route = Route(ApiEndpoints.ExchangeRates.Upsert, Clock.Today);
        (await Client.PutAsJsonAsync(route, new UpsertExchangeRateRequest { CupPerUsdCents = 52_000 }, Ct))
            .EnsureSuccessStatusCode();
        (await GetLogAsync()).Items.Should().BeEmpty();

        (await Client.PutAsJsonAsync(route, new UpsertExchangeRateRequest { CupPerUsdCents = 52_500 }, Ct))
            .EnsureSuccessStatusCode();

        var entry = (await GetLogAsync()).Items.Should().ContainSingle().Subject;
        entry.Subject.Should().Be(AuditSubjectCode.ExchangeRate);
        entry.Title.Should().Be("Tasa del dólar del 2026-11-12");
        entry.Changes.Should().BeEquivalentTo([
            new FieldChangeResponse { Field = "CUP por 1 USD", Before = "520.00 CUP", After = "525.00 CUP" }
        ]);
    }

    // ---------- Read from where it is kept ----------

    [Fact]
    public async Task Log_ShouldShowAVoidedPurchase_WithItsReason()
    {
        var (rice, supplier) = await Client.CreateRiceAsync(Ct);
        var purchase = await Client.CreatePurchaseAsync(
            await Client.UsdPurchaseRequestAsync(supplier.Id, Clock.Today, Ct, Sacks(rice.Id, 1, 4_000)), Ct);

        (await Client.PostAsJsonAsync(Route(ApiEndpoints.Purchases.Void, purchase.Id),
            new VoidPurchaseRequest { Reason = "Se cargó dos veces" }, Ct)).EnsureSuccessStatusCode();

        var entry = (await GetLogAsync()).Items.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(AuditEventKindCode.Voided);
        entry.Subject.Should().Be(AuditSubjectCode.Purchase);
        entry.SubjectId.Should().Be(purchase.Id);
        entry.Title.Should().Be($"Compra #{purchase.Number}");
        entry.Reason.Should().Be("Se cargó dos veces");
    }

    [Fact]
    public async Task Log_ShouldShowAPriceChange_ButNotTheFirstPrice()
    {
        var (rice, _) = await Client.CreateRiceAsync(Ct, salePriceCents: 25_000);

        (await Client.PutAsJsonAsync(Route(ApiEndpoints.Products.SalePrice, rice.Id),
            new SetSalePriceRequest { SalePriceCents = 27_000 }, Ct)).EnsureSuccessStatusCode();

        var entry = (await GetLogAsync()).Items.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(AuditEventKindCode.PriceChanged);
        entry.SubjectId.Should().Be(rice.Id);
        entry.Changes[0].Should().BeEquivalentTo(
            new FieldChangeResponse { Field = "Precio de venta", Before = "250.00 CUP", After = "270.00 CUP" });
    }

    [Fact]
    public async Task Log_ShouldShowANewProfitFirstPlan_ComparedWithThePreviousOne()
    {
        var overview = await Client.GetOverviewAsync(Ct);
        var response = await Client.PostAsJsonAsync(ApiEndpoints.AllocationPlans.Create, new CreateAllocationPlanRequest
        {
            EffectiveFrom = Clock.Today,
            Notes = "Más para la ganancia",
            Items =
            [
                new AllocationPlanItemRequest { EnvelopeId = overview.Envelope(EnvelopeType.Profit).Id, PercentBps = 1_000 },
                new AllocationPlanItemRequest { EnvelopeId = overview.Envelope(EnvelopeType.OwnerPay).Id, PercentBps = 4_500 },
                new AllocationPlanItemRequest { EnvelopeId = overview.Envelope(EnvelopeType.Tax).Id, PercentBps = 1_500 },
                new AllocationPlanItemRequest { EnvelopeId = overview.Envelope(EnvelopeType.Operations).Id, PercentBps = 3_000 }
            ]
        }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        var entry = (await GetLogAsync()).Items.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(AuditEventKindCode.ProfitFirstPlan);
        entry.Reason.Should().Be("Más para la ganancia");
        entry.Changes.Select(c => (c.Field, c.Before, c.After)).Should().BeEquivalentTo([
            ("Ganancia", "5 %", "10 %"),
            ("Compensación del dueño", "50 %", "45 %")
        ]);
    }

    // ---------- Filters ----------

    [Fact]
    public async Task Log_ShouldFilterByTheShopsDays_AndByKind_NewestFirst()
    {
        var box = await Client.GetCashBoxByNameAsync("Efectivo CUP", Ct);
        async Task RenameAsync(string name) =>
            (await Client.PutAsJsonAsync(Route(ApiEndpoints.CashBoxes.Update, box.Id),
                new UpdateCashBoxRequest { Name = name, IsActive = true }, Ct)).EnsureSuccessStatusCode();

        await RenameAsync("Caja 1");
        Clock.Now = Clock.Now.AddDays(1);
        await RenameAsync("Caja 2");
        (await SaveSettingsAsync(s => Same(s, address: "Calle 23"))).EnsureSuccessStatusCode();

        (await GetLogAsync()).Items.Select(e => e.Title).Should()
            .Equal("Caja «Caja 2»", "Datos del negocio y precios", "Caja «Caja 1»");
        (await GetLogAsync("?from=2026-11-12&to=2026-11-12")).Items.Select(e => e.Title).Should()
            .Equal("Caja «Caja 1»");
        (await GetLogAsync("?kind=Voided")).Items.Should().BeEmpty();

        var page = await GetLogAsync("?page=2&pageSize=2");
        page.Total.Should().Be(3);
        page.Items.Select(e => e.Title).Should().Equal("Caja «Caja 1»");
    }

    [Fact]
    public async Task Log_ShouldReturnBadRequest_WhenToIsBeforeFrom()
    {
        var response = await Client.GetAsync($"{ApiEndpoints.Audit.GetAll}?from=2026-11-12&to=2026-11-01", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
