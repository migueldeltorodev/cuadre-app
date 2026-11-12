namespace Cuadre.Api.Tests.Integration.Infrastructure;

/// <summary>
/// Every integration test class is decorated with [Collection(nameof(ApiCollection))]:
/// they share ONE ApiFactory (one host, one SQLite file) and run sequentially,
/// which is what makes the per-test database reset safe.
/// </summary>
[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
