using System.Net;
using Cuadre.Api.Tests.Integration.Infrastructure;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Health;

[Collection(nameof(ApiCollection))]
public class HealthCheckTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Health_ShouldReturnOk_WhenDatabaseIsReachable()
    {
        var response = await Client.GetAsync("_health", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
