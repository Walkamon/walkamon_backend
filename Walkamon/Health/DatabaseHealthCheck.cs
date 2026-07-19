using DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Walkamon.Health;

public sealed class DatabaseHealthCheck(
    WalkamonContext context,
    ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Database readiness check failed.");
            return HealthCheckResult.Unhealthy();
        }
    }
}
