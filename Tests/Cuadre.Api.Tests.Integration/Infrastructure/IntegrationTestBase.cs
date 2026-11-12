namespace Cuadre.Api.Tests.Integration.Infrastructure;

/// <summary>
/// Base for endpoint tests: resets the database before EACH test and exposes a ready-made client.
/// No auth in Cuadre (single local user), so there is only one client.
/// Concrete test classes MUST carry [Collection(nameof(ApiCollection))] themselves.
/// </summary>
public abstract class IntegrationTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; } = factory.CreateClient();

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await Factory.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected TestClock Clock => Factory.Clock;

    /// <summary>Route constants contain "{id:guid}"; this fills them in.</summary>
    protected static string Route(string template, Guid id) => template.Replace("{id:guid}", id.ToString());

    /// <summary>Route constants contain "{date}"; this fills them in as yyyy-MM-dd.</summary>
    protected static string Route(string template, DateOnly date) =>
        template.Replace("{date}", date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
}
