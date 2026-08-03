using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Walkamon.TransactionTests;

[Trait("UC", "UC-67")]
[Trait("UC", "UC-74")]
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
                Assert.Equal(2, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.pvp_matches') AND name IN ('finish_reason_code','forfeited_by_user_id')"));
                Assert.Equal(2, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.pvp_match_players') AND name IN ('pet_name_snapshot','pet_stage_no_snapshot')"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.pvp_bot_profiles') AND name='pet_stage_no'"));
                Assert.Equal(5, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.pvp_matches') AND name IN ('scoring_mode_code','daily_step_power_cap','base_pace_min_milli_steps_per_second','base_pace_max_milli_steps_per_second','last_progress_at')"));
                Assert.Equal(2, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.pvp_match_players') AND name IN ('daily_eligible_steps_snapshot','base_pace_milli_steps_per_second')"));
                Assert.Equal(0, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.tables WHERE object_id=OBJECT_ID('dbo.pvp_match_step_ledgers')"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.pvp_matches') AND name='FK_pvp_matches_forfeited_user'"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM dbo.system_settings WHERE setting_key='utc_pet_timestamp_backfill_v1'"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.tables WHERE object_id=OBJECT_ID('dbo.pvp_matchmaking_policies')"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM dbo.pvp_matchmaking_policies WHERE policy_version=1 AND is_active=1"));
                Assert.Equal(10, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.matchmaking_queue') AND name IN ('mmr_snapshot','daily_steps_snapshot','base_pace_snapshot','expected_distance_units','expected_speed_bps','policy_version','requires_relief','power_snapshot_at','bot_fallback_at','row_version')"));
                Assert.Equal(6, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.pvp_player_profiles') AND name IN ('consecutive_valid_ranked_losses','completed_ranked_matches_since_relief','last_relief_completed_at','last_bot_difficulty_code','consecutive_hard_bot_count','row_version')"));
                Assert.Equal(17, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.pvp_matches') AND name IN ('match_duration_seconds','matchmaking_policy_version','matchmaking_reason_code','bot_difficulty_code','is_relief_match','rating_policy_code','selection_roll_bps','expected_first_distance_units','expected_second_distance_units','expected_gap_bps','bot_reward_multiplier_bps','bot_win_mmr_delta','bot_draw_mmr_delta','bot_loss_mmr_delta','bot_rating_window','max_positive_bot_mmr_in_window','profile_state_applied_at')"));
                Assert.Equal(13, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.pvp_match_players') AND name IN ('expected_distance_units','expected_speed_bps','expected_passive_bps','expected_loadout_bps','passive_rule_bonus_bps_snapshot','passive_rule_start_minute_snapshot','passive_rule_end_minute_snapshot','bot_min_pace_snapshot','bot_max_pace_snapshot','ready_at','realtime_joined_at','streak_eligibility_code','row_version')"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.pvp_matches') AND name='FK_pvp_matches_matchmaking_policy'"));
                Assert.Equal(1, await ScalarAsync(connection,
                    "SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.pvp_matchmaking_policies') AND name='UX_pvp_matchmaking_policies_active' AND is_unique=1 AND has_filter=1"));

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
