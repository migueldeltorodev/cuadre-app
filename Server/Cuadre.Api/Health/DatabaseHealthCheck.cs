using Cuadre.Application.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cuadre.Api.Health;

public class DatabaseHealthCheck : IHealthCheck
{
    public const string Name = "Database";

    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(IDbConnectionFactory dbConnectionFactory, ILogger<DatabaseHealthCheck> logger)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var _ = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            const string errorMessage = "Database is unhealthy";
            _logger.LogError(ex, errorMessage);
            return HealthCheckResult.Unhealthy(errorMessage, ex);
        }
    }
}
