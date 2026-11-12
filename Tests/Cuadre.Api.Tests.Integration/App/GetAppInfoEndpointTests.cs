using System.Net;
using System.Net.Http.Json;
using Cuadre.Api.Tests.Integration.Infrastructure;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.App;

[Collection(nameof(ApiCollection))]
public class GetAppInfoEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetInfo_ShouldReturnOk_WithAppName()
    {
        // Act
        var response = await Client.GetAsync(ApiEndpoints.App.Info, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AppInfoResponse>(Ct);
        body!.Name.Should().Be("Cuadre");
        body.Environment.Should().Be("Testing");
    }

    [Fact]
    public async Task UnknownApiRoute_ShouldReturnNotFound_WhenNoEndpointMatches()
    {
        // The SPA fallback must never swallow API routes with index.html.
        var response = await Client.GetAsync($"{ApiEndpoints.ApiBase}/does-not-exist", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
