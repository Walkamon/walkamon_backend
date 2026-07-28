using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class PvpSchemaIntegrationTests
{
    [Fact]
    public async Task FreshSchema_AndUpgradeTwice_AreValidAndIdempotent()
    {
        var databaseName = $"WalkamonSchemaTests_{Guid.NewGuid():N}";
        var master = "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true";
        var database = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Sql");
        var freshSql = await File.ReadAllTextAsync(Path.Combine(sqlDirectory, "WalkamonFreshSchema.sql"));
        freshSql = freshSql
            .Replace("CREATE DATABASE Walkamon;", $"CREATE DATABASE [{databaseName}];", StringComparison.Ordinal)
            .Replace("USE Walkamon;", $"USE [{databaseName}];", StringComparison.Ordinal);
        var upgradeSql = await File.ReadAllTextAsync(Path.Combine(sqlDirectory, "pvp_sprint_upgrade.sql"));

        try
        {
            await using (var connection = new SqlConnection(master))
            {
                await connection.OpenAsync();
                await ExecuteBatchesAsync(connection, freshSql);
            }
            await using (var connection = new SqlConnection(database))
            {
                await connection.OpenAsync();
                await ExecuteBatchesAsync(connection, upgradeSql);
                await ExecuteBatchesAsync(connection, upgradeSql);

                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.tables WHERE object_id=OBJECT_ID('dbo.step_sensor_batches')"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.tables WHERE object_id=OBJECT_ID('dbo.step_motion_evidence_windows')"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.step_sensor_batches') AND name='motion_status'"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.pvp_matches') AND name='last_event_sequence'"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.pvp_step_sessions') AND name='UX_pvp_step_sessions_active_user' AND is_unique=1 AND has_filter=1"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.pvp_step_sessions') AND name='match_id' AND is_nullable=1"));
                Assert.Equal(4, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.user_pets') AND name IN ('energy_updated_at','bond_updated_at','life_force_updated_at','exp_updated_at')"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.pets') AND name='IX_pets_pvp_affinity_code' AND is_unique=0"));
                Assert.Equal(0, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.pets') AND name='UX_pets_pvp_affinity_code'"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.tables WHERE object_id=OBJECT_ID('dbo.pvp_match_reward_snapshots')"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.tables WHERE object_id=OBJECT_ID('dbo.pvp_match_reward_snapshot_items')"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.pvp_match_reward_snapshots') AND name='UX_pvp_match_reward_snapshots_match_result' AND is_unique=1"));

                await using var duplicateAffinityCommand = connection.CreateCommand();
                duplicateAffinityCommand.CommandText = """
                    INSERT dbo.pets(pet_name, pvp_affinity_code)
                    VALUES (N'Affinity regression A', 'sprout'),
                           (N'Affinity regression B', 'sprout');
                    """;
                Assert.Equal(2, await duplicateAffinityCommand.ExecuteNonQueryAsync());
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(master);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF DB_ID(N'{databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END
                """;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task ExecuteBatchesAsync(SqlConnection connection, string script)
    {
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*;?\s*$",
                     RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 60;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
