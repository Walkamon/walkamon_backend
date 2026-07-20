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
            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("SQL Server is unreachable.");
            }

            var schemaIsReady = await context.Database
                .SqlQueryRaw<int>(
                    """
                    SELECT CASE
                        WHEN COL_LENGTH('dbo.pvp_matches', 'item_slot_limit') IS NOT NULL
                         AND COL_LENGTH('dbo.pvp_matches', 'last_event_sequence') IS NOT NULL
                         AND COL_LENGTH('dbo.pvp_matches', 'row_version') IS NOT NULL
                         AND COL_LENGTH('dbo.pvp_matches', 'rule_version') IS NOT NULL
                         AND COL_LENGTH('dbo.pvp_matches', 'speed_min_bps') IS NOT NULL
                         AND COL_LENGTH('dbo.pvp_matches', 'speed_max_bps') IS NOT NULL
                         AND OBJECT_ID('dbo.outbox_events', 'U') IS NOT NULL
                        THEN 1
                        ELSE 0
                    END AS [Value]
                    """)
                .SingleAsync(cancellationToken) == 1;

            return schemaIsReady
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Required production database schema is missing.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Database readiness check failed.");
            return HealthCheckResult.Unhealthy();
        }
    }
}
