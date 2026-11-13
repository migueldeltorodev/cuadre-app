using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Closings;

[Collection(nameof(ApiCollection))]
public class StaffEndpointsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<EmployeeResponse> CreateEmployeeAsync(string name)
    {
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Employees.Create,
            new CreateEmployeeRequest { Name = name }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>(Ct))!;
    }

    // ---------- Employees ----------

    [Fact]
    public async Task CreateEmployee_ShouldAddAnActiveEmployee()
    {
        var employee = await CreateEmployeeAsync("  Yeni  ");

        employee.Name.Should().Be("Yeni");
        employee.IsActive.Should().BeTrue();
        var all = (await Client.GetFromJsonAsync<EmployeesResponse>(ApiEndpoints.Employees.GetAll, Ct))!;
        all.Items.Should().ContainSingle(x => x.Id == employee.Id);
    }

    [Fact]
    public async Task CreateEmployee_ShouldReturn409_WhenTheNameIsTaken()
    {
        await CreateEmployeeAsync("Yeni");

        var response = await Client.PostAsJsonAsync(ApiEndpoints.Employees.Create,
            new CreateEmployeeRequest { Name = "Yeni" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateEmployee_ShouldReturn400_WhenTheNameIsEmpty()
    {
        var response = await Client.PostAsJsonAsync(ApiEndpoints.Employees.Create,
            new CreateEmployeeRequest { Name = " " }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateEmployee_ShouldRenameAndDeactivate()
    {
        var employee = await CreateEmployeeAsync("Yeni");

        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Employees.Update, employee.Id),
            new UpdateEmployeeRequest { Name = "Yenisleidy", IsActive = false }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<EmployeeResponse>(Ct))!;
        updated.Name.Should().Be("Yenisleidy");
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateEmployee_ShouldReturn404_WhenItDoesNotExist()
    {
        var response = await Client.PutAsJsonAsync(Route(ApiEndpoints.Employees.Update, Guid.NewGuid()),
            new UpdateEmployeeRequest { Name = "Nadie", IsActive = true }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Pay schemes ----------

    [Fact]
    public async Task PaySchemes_ShouldStartWithTheSeededOne()
    {
        var schemes = (await Client.GetFromJsonAsync<StaffPaySchemesResponse>(ApiEndpoints.StaffPaySchemes.GetAll,
            Ct))!;

        var seeded = schemes.Items.Should().ContainSingle().Subject;
        seeded.DailyFixedCents.Should().Be(100_000);
        seeded.PercentOfSalesBps.Should().Be(100);
    }

    [Fact]
    public async Task CreatePayScheme_ShouldAddANewScheme_AndReplaceIt_WhenTheDateIsTheSame()
    {
        var request = new CreateStaffPaySchemeRequest
        {
            EffectiveFrom = Clock.Today,
            DailyFixedCents = 120_000,
            PercentOfSalesBps = 150
        };
        (await Client.PostAsJsonAsync(ApiEndpoints.StaffPaySchemes.Create, request, Ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var fixedTypo = await Client.PostAsJsonAsync(ApiEndpoints.StaffPaySchemes.Create,
            new CreateStaffPaySchemeRequest { EffectiveFrom = Clock.Today, DailyFixedCents = 110_000, PercentOfSalesBps = 150 },
            Ct);
        fixedTypo.StatusCode.Should().Be(HttpStatusCode.Created);

        var schemes = (await Client.GetFromJsonAsync<StaffPaySchemesResponse>(ApiEndpoints.StaffPaySchemes.GetAll,
            Ct))!;
        schemes.Items.Should().HaveCount(2);
        schemes.Items.First().DailyFixedCents.Should().Be(110_000);
    }

    [Fact]
    public async Task CreatePayScheme_ShouldReturn422_WhenItStartsBeforeTheLatestOne()
    {
        (await Client.PostAsJsonAsync(ApiEndpoints.StaffPaySchemes.Create, new CreateStaffPaySchemeRequest
        {
            EffectiveFrom = Clock.Today,
            DailyFixedCents = 120_000,
            PercentOfSalesBps = 100
        }, Ct)).StatusCode.Should().Be(HttpStatusCode.Created);

        var earlier = await Client.PostAsJsonAsync(ApiEndpoints.StaffPaySchemes.Create, new CreateStaffPaySchemeRequest
        {
            EffectiveFrom = Clock.Today.AddDays(-1),
            DailyFixedCents = 120_000,
            PercentOfSalesBps = 100
        }, Ct);

        earlier.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreatePayScheme_ShouldReturn400_WhenThePercentIsAbove100()
    {
        var response = await Client.PostAsJsonAsync(ApiEndpoints.StaffPaySchemes.Create, new CreateStaffPaySchemeRequest
        {
            EffectiveFrom = Clock.Today,
            DailyFixedCents = 100_000,
            PercentOfSalesBps = 10_001
        }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
