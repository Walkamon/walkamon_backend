using BLL.Options;
using BLL.Service;
using BLL.Interfaces;
using DAL.Data;
using DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Xunit;

namespace Walkamon.IntegrationTests.StepTracking;

public sealed class SimpleTemporalPolicyBAuthoritativeTests
{
    [Theory]
    [InlineData(16_999, 1, "ALLOW", 80)]
    [InlineData(17_000, 1, "BLOCK", 0)]
    [InlineData(1_000, 2, "BLOCK", 0)]
    [InlineData(0, 0, "ALLOW", 80)]
    public void FrozenPolicyBUsesInclusiveDurationOrRegionVeto(
        long durationMs,
        int regionCount,
        string decision,
        long eligible)
    {
        var result = Evaluate(80, durationMs, regionCount);

        Assert.Equal(decision, result.Decision);
        Assert.Equal(eligible, result.EligibleStepCount);
        Assert.Equal(SimpleTemporalPolicyBConstants.Revision, result.PolicyRevision);
        Assert.Equal(SimpleTemporalPolicyBConstants.Provenance, result.PolicyProvenance);
    }

    [Fact]
    public void BothFraudConditionsPersistBothReadableReasons()
    {
        var result = Evaluate(120, 17_000, 2);

        Assert.Equal(SimpleTemporalPolicyDecisions.Block, result.Decision);
        Assert.Contains(SimpleTemporalPolicyBReasonCodes.SustainedFraud, result.ReasonCodes);
        Assert.Contains(SimpleTemporalPolicyBReasonCodes.RepeatedFraud, result.ReasonCodes);
        Assert.Equal(0, result.EligibleStepCount);
    }

    [Fact]
    public void SecurityOrStructuralFailureCanNeverBecomeAuthoritative()
    {
        var security = SimpleTemporalPolicyB.Evaluate(new(80, 0, 0, SecurityValid: false));
        var structure = SimpleTemporalPolicyB.Evaluate(new(
            80, 0, 0, SecurityValid: true, CounterIntervalValid: false));

        Assert.Equal(SimpleTemporalPolicyDecisions.Block, security.Decision);
        Assert.Contains(SimpleTemporalPolicyBReasonCodes.SecurityFailed, security.ReasonCodes);
        Assert.Equal(SimpleTemporalPolicyDecisions.Block, structure.Decision);
        Assert.Contains(SimpleTemporalPolicyBReasonCodes.CounterIntervalInvalid,
            structure.ReasonCodes);
    }

    [Fact]
    public void KillSwitchAndRetryApplyAnAllowedIntervalExactlyOnce()
    {
        var policy = Evaluate(80, 0, 0);
        var disabled = SimpleAuthoritativeTransitionRules.Evaluate(
            policy, authoritativeEnabled: false, alreadyAuthoritative: false);
        Assert.False(disabled.IsNewAuthoritativeTransition);
        Assert.Equal(0, disabled.NewlyAuthoritativeSteps);

        var total = 0L;
        var alreadyAuthoritative = false;
        for (var retry = 0; retry < 10; retry++)
        {
            var transition = SimpleAuthoritativeTransitionRules.Evaluate(
                policy, authoritativeEnabled: true, alreadyAuthoritative);
            total += transition.NewlyAuthoritativeSteps;
            alreadyAuthoritative |= transition.IsNewAuthoritativeTransition;
        }

        Assert.Equal(80, total);
    }

    [Fact]
    public void BlockNeverCreatesAnAuthoritativeTransition()
    {
        var transition = SimpleAuthoritativeTransitionRules.Evaluate(
            Evaluate(120, 17_000, 1),
            authoritativeEnabled: true,
            alreadyAuthoritative: false);

        Assert.False(transition.IsNewAuthoritativeTransition);
        Assert.Equal(0, transition.NewlyAuthoritativeSteps);
    }

    [Fact]
    public void LateMotionLifecycleMustCloseBeforePolicyFinalization()
    {
        var now = new DateTime(2026, 8, 12, 10, 0, 30, DateTimeKind.Utc);
        var endpoint = now.AddSeconds(-20);

        Assert.False(SimpleTemporalSettlementRules.IsFinal(
            endpoint, now, counterSettlementSeconds: 15, pendingDetectorCount: 1));
        Assert.True(SimpleTemporalSettlementRules.IsFinal(
            endpoint, now, counterSettlementSeconds: 15, pendingDetectorCount: 0));
        Assert.False(SimpleTemporalSettlementRules.IsFinal(
            now.AddSeconds(-14), now, counterSettlementSeconds: 15, pendingDetectorCount: 0));
    }

    [Fact]
    public void CounterFactoryKeepsBaselineAndRebootSafetyPolicy()
    {
        var bootA = Guid.NewGuid();
        var bootB = Guid.NewGuid();
        var baseline = new SimpleCounterObservation(Guid.NewGuid(), bootA, 1_000, 5_000);

        Assert.Null(SimpleCounterIntervalFactory.Create(null, baseline));
        Assert.Null(SimpleCounterIntervalFactory.Create(
            baseline,
            new(Guid.NewGuid(), bootB, 500, 20)));
        Assert.Null(SimpleCounterIntervalFactory.Create(
            baseline,
            new(Guid.NewGuid(), bootA, 2_000, 4_999)));
        Assert.Equal(80, SimpleCounterIntervalFactory.Create(
            baseline,
            new(Guid.NewGuid(), bootA, 2_000, 5_080))!.CounterDelta);
    }

    [Fact]
    public void ConfigurationFailsSafeForUnknownOrDoubleAuthority()
    {
        Assert.Throws<InvalidOperationException>(() =>
            StepValidationConfigurationValidator.Validate(new()
            {
                V3AuthoritativeEnabled = true,
                SimpleStepValidationEnabled = true,
                SimpleStepValidationRevision = SimpleTemporalPolicyBConstants.Revision,
                SimpleStepValidationAuthoritativeEnabled = true
            }));
        Assert.Throws<InvalidOperationException>(() =>
            StepValidationConfigurationValidator.Validate(new()
            {
                SimpleStepValidationEnabled = false,
                SimpleStepValidationAuthoritativeEnabled = true
            }));
        Assert.Throws<InvalidOperationException>(() =>
            StepValidationConfigurationValidator.Validate(new()
            {
                SimpleStepValidationEnabled = true,
                SimpleStepValidationRevision = "unknown"
            }));

        StepValidationConfigurationValidator.Validate(new()
        {
            V3AuthoritativeEnabled = false,
            SimpleStepValidationEnabled = true,
            SimpleStepValidationRevision = SimpleTemporalPolicyBConstants.Revision,
            SimpleStepValidationAuthoritativeEnabled = true
        });
    }

    [Fact]
    public void CodeDefaultsKeepBothAuthoritativePipelinesOff()
    {
        var options = new StepValidationOptions();

        Assert.False(options.V3AuthoritativeEnabled);
        Assert.False(options.SimpleStepValidationAuthoritativeEnabled);
        Assert.Equal(SimpleTemporalPolicyBConstants.Revision,
            options.SimpleStepValidationRevision);
        StepValidationConfigurationValidator.Validate(options);
    }

    [Fact]
    public void ExistingSchemaProvidesStableUniqueAuthoritativeLedgerKey()
    {
        var options = new DbContextOptionsBuilder<WalkamonContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=WalkamonModelOnly")
            .Options;
        using var context = new WalkamonContext(options, new HttpContextAccessor());
        var entity = context.Model.FindEntityType(typeof(ValidatedStepRecord))!;
        var ledgerIndex = entity.GetIndexes().Single(index =>
            index.IsUnique &&
            index.Properties.Select(x => x.Name).SequenceEqual(
                [nameof(ValidatedStepRecord.UserId), nameof(ValidatedStepRecord.PayloadHash)]));

        Assert.True(ledgerIndex.IsUnique);
        Assert.Equal(64, entity.FindProperty(nameof(ValidatedStepRecord.PayloadHash))!.GetMaxLength());
    }

    [Fact]
    public async Task ConcurrentIntervalTransitionsIncrementDailyTotalOnce()
    {
        var databaseName = $"WalkamonSimpleConcurrency_{Guid.NewGuid():N}";
        var master =
            "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true";
        var connectionString =
            $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
        var userId = Guid.NewGuid();
        var intervalHash = new string('A', 64);

        await using (var connection = new SqlConnection(master))
        {
            await connection.OpenAsync();
            await new SqlCommand($"CREATE DATABASE [{databaseName}]", connection)
                .ExecuteNonQueryAsync();
        }

        try
        {
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                const string schema = """
                    CREATE TABLE daily_steps (
                        user_id UNIQUEIDENTIFIER NOT NULL,
                        step_date DATE NOT NULL,
                        step_count INT NOT NULL,
                        eligible_step_count INT NOT NULL,
                        PRIMARY KEY (user_id, step_date));
                    CREATE TABLE validated_step_records (
                        validated_step_record_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        user_id UNIQUEIDENTIFIER NOT NULL,
                        payload_hash CHAR(64) NOT NULL);
                    CREATE UNIQUE INDEX UX_validated_step_records_user_hash
                        ON validated_step_records(user_id, payload_hash);
                    """;
                await new SqlCommand(schema, connection).ExecuteNonQueryAsync();
                await new SqlCommand(
                    "INSERT INTO daily_steps VALUES (@user, '2026-08-12', 0, 0)",
                    connection)
                {
                    Parameters = { new SqlParameter("@user", userId) }
                }.ExecuteNonQueryAsync();
            }

            async Task<bool> ApplyAsync()
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync(
                    IsolationLevel.Serializable);
                try
                {
                    var insert = new SqlCommand("""
                        INSERT INTO validated_step_records
                            (validated_step_record_id, user_id, payload_hash)
                        VALUES (NEWID(), @user, @hash)
                        """, connection, (SqlTransaction)transaction);
                    insert.Parameters.AddWithValue("@user", userId);
                    insert.Parameters.AddWithValue("@hash", intervalHash);
                    await insert.ExecuteNonQueryAsync();

                    var update = new SqlCommand("""
                        UPDATE daily_steps
                        SET step_count = step_count + 80,
                            eligible_step_count = eligible_step_count + 80
                        WHERE user_id = @user AND step_date = '2026-08-12'
                        """, connection, (SqlTransaction)transaction);
                    update.Parameters.AddWithValue("@user", userId);
                    await update.ExecuteNonQueryAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (SqlException exception) when (exception.Number is 2601 or 2627)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }

            var outcomes = await Task.WhenAll(ApplyAsync(), ApplyAsync());

            await using var verify = new SqlConnection(connectionString);
            await verify.OpenAsync();
            var daily = Convert.ToInt32(await new SqlCommand(
                "SELECT eligible_step_count FROM daily_steps WHERE user_id = @user",
                verify)
            {
                Parameters = { new SqlParameter("@user", userId) }
            }.ExecuteScalarAsync());
            var ledgerRows = Convert.ToInt32(await new SqlCommand(
                "SELECT COUNT(*) FROM validated_step_records WHERE user_id = @user AND payload_hash = @hash",
                verify)
            {
                Parameters =
                {
                    new SqlParameter("@user", userId),
                    new SqlParameter("@hash", intervalHash)
                }
            }.ExecuteScalarAsync());

            Assert.Single(outcomes, value => value);
            Assert.Equal(80, daily);
            Assert.Equal(1, ledgerRows);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(master);
            await connection.OpenAsync();
            await new SqlCommand($"DROP DATABASE IF EXISTS [{databaseName}]", connection)
                .ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public void AggregateResultContainsNoPerStepIdentityOrSyntheticTimestamp()
    {
        var properties = typeof(SimpleTemporalPolicyBResult)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain(properties, x =>
            x.Contains("Detector", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("EventId", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Timestamp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NewlyAuthoritativeAggregateUsesExistingProgressPipeline()
    {
        var userId = Guid.NewGuid();
        var achievements = new RecordingAchievementProgress();
        var missions = new RecordingMissionProgress();

        await ValidatedStepService.SyncAcceptedProgressAsync(
            userId,
            acceptedSteps: 80,
            newPetLevel: null,
            achievements,
            missions);

        Assert.Equal([(userId, MissionMetricCodeCatalog.Steps, 80)],
            achievements.Added);
        Assert.Equal([(userId, MissionMetricCodeCatalog.Steps, 80)], missions.Added);
        Assert.Empty(achievements.Maxima);
        Assert.Empty(missions.Maxima);
    }

    [Fact]
    public async Task BlockedOrDuplicateZeroDeltaProducesNoProgressSideEffect()
    {
        var achievements = new RecordingAchievementProgress();
        var missions = new RecordingMissionProgress();

        await ValidatedStepService.SyncAcceptedProgressAsync(
            Guid.NewGuid(), 0, null, achievements, missions);

        Assert.Empty(achievements.Added);
        Assert.Empty(missions.Added);
    }

    [Theory]
    [MemberData(nameof(DevelopmentGoldenRows))]
    public void DevelopmentDatasetKeepsFrozenPolicyBDecision(
        string trialId,
        long maxDurationMs,
        int regions,
        string expected)
    {
        Assert.False(string.IsNullOrWhiteSpace(trialId));
        Assert.Equal(expected, Evaluate(100, maxDurationMs, regions).Decision);
    }

    public static IEnumerable<object[]> DevelopmentGoldenRows()
    {
        yield return Row("normal-hand-01", 0, 0, "ALLOW");
        yield return Row("normal-hand-02", 0, 0, "ALLOW");
        yield return Row("normal-hand-03", 0, 0, "ALLOW");
        yield return Row("slow-hand-01", 0, 0, "ALLOW");
        yield return Row("slow-hand-02", 0, 0, "ALLOW");
        yield return Row("slow-hand-03", 0, 0, "ALLOW");
        yield return Row("fast-hand-01", 0, 0, "ALLOW");
        yield return Row("fast-hand-02", 0, 0, "ALLOW");
        yield return Row("fast-hand-03", 0, 0, "ALLOW");
        yield return Row("normal-pocket-01", 0, 0, "ALLOW");
        yield return Row("normal-pocket-02", 1_000, 1, "ALLOW");
        yield return Row("normal-pocket-03", 0, 0, "ALLOW");
        yield return Row("slow-pocket-01", 0, 0, "ALLOW");
        yield return Row("slow-pocket-02", 0, 0, "ALLOW");
        yield return Row("slow-pocket-03", 1_000, 1, "ALLOW");
        yield return Row("shake-light-01", 22_000, 5, "BLOCK");
        yield return Row("shake-light-02", 4_000, 12, "BLOCK");
        yield return Row("shake-light-03", 6_000, 4, "BLOCK");
        yield return Row("shake-hard-01", 17_000, 2, "BLOCK");
        yield return Row("shake-hard-02", 40_000, 1, "BLOCK");
        yield return Row("shake-hard-03", 58_000, 1, "BLOCK");
    }

    private static object[] Row(string id, long duration, int regions, string expected) =>
        [id, duration, regions, expected];

    private static SimpleTemporalPolicyBResult Evaluate(
        long counterDelta,
        long maxDurationMs,
        int regions) => SimpleTemporalPolicyB.Evaluate(new(
        counterDelta,
        regions,
        maxDurationMs));

    private sealed class RecordingAchievementProgress : IAchievementProgressService
    {
        public List<(Guid UserId, string Metric, int Amount)> Added { get; } = [];
        public List<(Guid UserId, string Metric, int Value)> Maxima { get; } = [];

        public Task AddProgressAsync(Guid userId, string metricCode, int amount)
        {
            Added.Add((userId, metricCode, amount));
            return Task.CompletedTask;
        }

        public Task SetProgressMaxAsync(Guid userId, string metricCode, int value)
        {
            Maxima.Add((userId, metricCode, value));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMissionProgress : IMissionProgressService
    {
        public List<(Guid UserId, string Metric, int Amount)> Added { get; } = [];
        public List<(Guid UserId, string Metric, int Value)> Maxima { get; } = [];

        public Task AddProgressAsync(Guid userId, string metricCode, int amount)
        {
            Added.Add((userId, metricCode, amount));
            return Task.CompletedTask;
        }

        public Task SetProgressMaxAsync(Guid userId, string metricCode, int value)
        {
            Maxima.Add((userId, metricCode, value));
            return Task.CompletedTask;
        }

        public Task<bool> ArePrerequisitesMetAsync(Guid userId, Guid missionId) =>
            Task.FromResult(true);
    }
}
