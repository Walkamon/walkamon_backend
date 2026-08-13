using System.Data;
using System.Text.RegularExpressions;
using BLL.Exceptions;
using BLL.Interfaces;
using BLL.Options;
using BLL.Service;
using DAL.Data;
using DAL.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Walkamon.IntegrationTests.StepTracking;

[Collection(SimpleAuthoritativeSqlCollection.Name)]
public sealed class SimpleAuthoritativeLocalActivationTests(
    SimpleAuthoritativeSqlFixture fixture)
{
    [Fact]
    public async Task AllowAppliesCounterDeltaAndExistingGameplayPipelineExactlyOnce()
    {
        var userId = await fixture.SeedUserAsync(withPet: true);
        var before = await fixture.ReadSnapshotAsync(userId);
        var segment = await PrepareOpenSegmentAsync(userId, 50, authoritativeEnabled: true);

        await fixture.AgeEvidenceAsync(segment.SessionId, latestEvidenceSequence: 2);
        var heartbeat = BuildRequest(segment, sequence: 3);
        PvpStepBatchResponse response;
        await using (var context = fixture.CreateContext())
        {
            response = await CreateService(context, authoritativeEnabled: true)
                .SubmitDailyBatchAsync(userId, segment.SessionId, heartbeat);
        }

        var after = await fixture.ReadSnapshotAsync(userId);
        Assert.Equal(0, before.DailyEligible);
        Assert.Equal(50, after.DailySteps);
        Assert.Equal(50, after.DailyEligible);
        Assert.Equal(1, after.SimpleSegmentCount);
        Assert.Equal(50, after.SimpleSegmentRawSteps);
        Assert.Equal(50, after.SimpleSegmentEligibleSteps);
        Assert.Equal(50, after.MissionProgress);
        Assert.Equal(50, after.AchievementProgress);
        Assert.Equal(6, after.PetExperience);
        Assert.Equal(before.PetLifeForce, after.PetLifeForce);
        Assert.Equal(before.WalletBalance, after.WalletBalance);
        Assert.Equal(before.InventoryQuantity, after.InventoryQuantity);
        Assert.Equal(before.PvpEventCount, after.PvpEventCount);
        Assert.Equal(50, response.DailyAcceptedTotal);
        Assert.Empty(heartbeat.DetectorEvents);
        Assert.Empty(heartbeat.CounterSamples);
        Assert.Equal(0, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.step_counter_evidence_samples WHERE batch_id = @batchId",
            new SqlParameter("@batchId", response.BatchId)));
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.validated_step_records WHERE user_id = @userId AND source_code = 'simple_counter_segment' AND validation_status = 'accepted'",
            new SqlParameter("@userId", userId)));
    }

    [Fact]
    public async Task LateFraudBeforeSettlementBlocksWholeSegmentAndAllGameplaySideEffects()
    {
        var userId = await fixture.SeedUserAsync(withPet: true);
        var before = await fixture.ReadSnapshotAsync(userId);
        var segment = await PrepareOpenSegmentAsync(userId, 80, authoritativeEnabled: true);

        var lateMotion = BuildRequest(
            segment,
            sequence: 3,
            motionWindows:
            [
                HardShake(segment, startSecond: 4),
                HardShake(segment, startSecond: 14)
            ]);
        await using (var context = fixture.CreateContext())
        {
            var open = await CreateService(context, authoritativeEnabled: true)
                .SubmitDailyBatchAsync(userId, segment.SessionId, lateMotion);
            Assert.Equal("pending_reconciliation", open.ReconciliationStatus);
        }

        await fixture.AgeEvidenceAsync(segment.SessionId, latestEvidenceSequence: 3);
        var heartbeat = BuildRequest(segment, sequence: 4);
        await using (var context = fixture.CreateContext())
        {
            await CreateService(context, authoritativeEnabled: true)
                .SubmitDailyBatchAsync(userId, segment.SessionId, heartbeat);
        }

        var after = await fixture.ReadSnapshotAsync(userId);
        var persistedMotionReasons = await fixture.ScalarAsync<string>(
            "SELECT STRING_AGG(reason_codes, '|') FROM dbo.step_motion_evidence_windows w JOIN dbo.step_sensor_batches b ON b.step_sensor_batch_id = w.batch_id WHERE b.step_session_id = @sessionId",
            new SqlParameter("@sessionId", segment.SessionId));
        var segmentAuditReason = await fixture.ScalarAsync<string>(
            "SELECT rejection_reason FROM dbo.validated_step_records WHERE user_id = @userId AND source_code = 'simple_counter_segment'",
            new SqlParameter("@userId", userId));
        Assert.True(
            after.DailyEligible == before.DailyEligible,
            $"Late hard-shake evidence did not block: motion={persistedMotionReasons}; segment={segmentAuditReason}; daily={after.DailyEligible}");
        Assert.Equal(before.MissionProgress, after.MissionProgress);
        Assert.Equal(before.AchievementProgress, after.AchievementProgress);
        Assert.Equal(before.PetExperience, after.PetExperience);
        Assert.Equal(before.PetLifeForce, after.PetLifeForce);
        Assert.Equal(before.WalletBalance, after.WalletBalance);
        Assert.Equal(before.InventoryQuantity, after.InventoryQuantity);
        Assert.Equal(before.PvpEventCount, after.PvpEventCount);
        Assert.Equal(1, after.SimpleSegmentCount);
        Assert.Equal(80, after.SimpleSegmentRawSteps);
        Assert.Equal(0, after.SimpleSegmentEligibleSteps);
        Assert.Equal(2, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.step_motion_evidence_windows w JOIN dbo.step_sensor_batches b ON b.step_sensor_batch_id = w.batch_id WHERE b.step_session_id = @sessionId",
            new SqlParameter("@sessionId", segment.SessionId)));
        Assert.Equal("rejected", await fixture.ScalarAsync<string>(
            "SELECT validation_status FROM dbo.validated_step_records WHERE user_id = @userId AND source_code = 'simple_counter_segment'",
            new SqlParameter("@userId", userId)));
    }

    [Fact]
    public async Task TenRetriesOfSameResolutionRemainExactlyOnce()
    {
        var userId = await fixture.SeedUserAsync(withPet: true);
        var segment = await PrepareOpenSegmentAsync(userId, 50, authoritativeEnabled: true);
        await fixture.AgeEvidenceAsync(segment.SessionId, latestEvidenceSequence: 2);
        var heartbeat = BuildRequest(segment, sequence: 3);

        for (var attempt = 0; attempt <= 10; attempt++)
        {
            await using var context = fixture.CreateContext();
            await CreateService(context, authoritativeEnabled: true)
                .SubmitDailyBatchAsync(userId, segment.SessionId, heartbeat);
        }

        var snapshot = await fixture.ReadSnapshotAsync(userId);
        Assert.Equal(50, snapshot.DailyEligible);
        Assert.Equal(50, snapshot.MissionProgress);
        Assert.Equal(50, snapshot.AchievementProgress);
        Assert.Equal(6, snapshot.PetExperience);
        Assert.Equal(1, snapshot.SimpleSegmentCount);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.step_sensor_batches WHERE step_session_id = @sessionId AND sequence = 3",
            new SqlParameter("@sessionId", segment.SessionId)));
    }

    [Fact]
    public async Task ConcurrentDuplicateResolutionCommitsOneAuthoritativeTransition()
    {
        var userId = await fixture.SeedUserAsync(withPet: false);
        var segment = await PrepareOpenSegmentAsync(userId, 50, authoritativeEnabled: true);
        await fixture.AgeEvidenceAsync(segment.SessionId, latestEvidenceSequence: 2);
        var heartbeat = BuildRequest(segment, sequence: 3);

        async Task<PvpStepBatchResponse> ResolveAsync()
        {
            await using var context = fixture.CreateContext();
            return await CreateService(context, authoritativeEnabled: true)
                .SubmitDailyBatchAsync(userId, segment.SessionId, heartbeat);
        }

        var responses = await Task.WhenAll(ResolveAsync(), ResolveAsync());
        var snapshot = await fixture.ReadSnapshotAsync(userId);
        Assert.All(responses, response => Assert.Equal(50, response.DailyAcceptedTotal));
        Assert.Equal(50, snapshot.DailyEligible);
        Assert.Equal(50, snapshot.MissionProgress);
        Assert.Equal(50, snapshot.AchievementProgress);
        Assert.Equal(1, snapshot.SimpleSegmentCount);
        Assert.Equal(1, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.step_sensor_batches WHERE step_session_id = @sessionId AND sequence = 3",
            new SqlParameter("@sessionId", segment.SessionId)));
    }

    [Fact]
    public async Task FailureBeforeCommitRollsEverythingBackAndRetryAppliesOnce()
    {
        var userId = await fixture.SeedUserAsync(withPet: true);
        var segment = await PrepareOpenSegmentAsync(userId, 50, authoritativeEnabled: true);
        await fixture.AgeEvidenceAsync(segment.SessionId, latestEvidenceSequence: 2);
        var heartbeat = BuildRequest(segment, sequence: 3);

        await using (var context = fixture.CreateContext())
        {
            var service = CreateService(
                context,
                authoritativeEnabled: true,
                missionProgressService: new ThrowingMissionProgressService());
            await Assert.ThrowsAsync<InjectedGameplayFailureException>(() =>
                service.SubmitDailyBatchAsync(userId, segment.SessionId, heartbeat));
        }

        var rolledBack = await fixture.ReadSnapshotAsync(userId);
        Assert.Equal(0, rolledBack.DailyEligible);
        Assert.Equal(0, rolledBack.SimpleSegmentCount);
        Assert.Equal(0, rolledBack.MissionProgress);
        Assert.Equal(0, rolledBack.AchievementProgress);
        Assert.Equal(0, rolledBack.PetExperience);
        Assert.Equal(2, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.step_sensor_batches WHERE step_session_id = @sessionId",
            new SqlParameter("@sessionId", segment.SessionId)));

        await using (var context = fixture.CreateContext())
        {
            await CreateService(context, authoritativeEnabled: true)
                .SubmitDailyBatchAsync(userId, segment.SessionId, heartbeat);
        }

        var retried = await fixture.ReadSnapshotAsync(userId);
        Assert.Equal(50, retried.DailyEligible);
        Assert.Equal(1, retried.SimpleSegmentCount);
        Assert.Equal(50, retried.MissionProgress);
        Assert.Equal(50, retried.AchievementProgress);
        Assert.Equal(6, retried.PetExperience);
    }

    [Fact]
    public async Task SecurityFailureCannotFinalizeOtherwiseAllowedSegment()
    {
        var userId = await fixture.SeedUserAsync(withPet: true);
        var segment = await PrepareOpenSegmentAsync(userId, 50, authoritativeEnabled: true);
        await fixture.AgeEvidenceAsync(segment.SessionId, latestEvidenceSequence: 2);
        var invalid = BuildRequest(segment, sequence: 3);
        invalid.PayloadHash = new string('0', 64);

        await using (var context = fixture.CreateContext())
        {
            await Assert.ThrowsAsync<BadRequestException>(() =>
                CreateService(context, authoritativeEnabled: true)
                    .SubmitDailyBatchAsync(userId, segment.SessionId, invalid));
        }

        var snapshot = await fixture.ReadSnapshotAsync(userId);
        Assert.Equal(0, snapshot.DailyEligible);
        Assert.Equal(0, snapshot.SimpleSegmentCount);
        Assert.Equal(0, snapshot.MissionProgress);
        Assert.Equal(0, snapshot.AchievementProgress);
        Assert.Equal(0, snapshot.PetExperience);
        Assert.Equal(2, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.step_sensor_batches WHERE step_session_id = @sessionId",
            new SqlParameter("@sessionId", segment.SessionId)));
    }

    [Fact]
    public async Task KillSwitchKeepsAllowAsShadowAndProducesNoGameplayDelta()
    {
        var userId = await fixture.SeedUserAsync(withPet: true);
        var segment = await PrepareOpenSegmentAsync(userId, 70, authoritativeEnabled: false);
        await fixture.AgeEvidenceAsync(segment.SessionId, latestEvidenceSequence: 2);
        var heartbeat = BuildRequest(segment, sequence: 3);

        await using (var context = fixture.CreateContext())
        {
            await CreateService(context, authoritativeEnabled: false)
                .SubmitDailyBatchAsync(userId, segment.SessionId, heartbeat);
        }

        var snapshot = await fixture.ReadSnapshotAsync(userId);
        Assert.Equal(0, snapshot.DailyEligible);
        Assert.Equal(1, snapshot.SimpleSegmentCount);
        Assert.Equal(70, snapshot.SimpleSegmentRawSteps);
        Assert.Equal(0, snapshot.SimpleSegmentEligibleSteps);
        Assert.Equal(0, snapshot.MissionProgress);
        Assert.Equal(0, snapshot.AchievementProgress);
        Assert.Equal(0, snapshot.PetExperience);
        Assert.Contains("simple_authoritative_kill_switch_off",
            await fixture.ScalarAsync<string>(
                "SELECT rejection_reason FROM dbo.validated_step_records WHERE user_id = @userId AND source_code = 'simple_counter_segment'",
                new SqlParameter("@userId", userId)));
    }

    [Fact]
    public async Task FirstSampleAndNewBootRemainBaselinesWithoutCrossBootDelta()
    {
        var userId = await fixture.SeedUserAsync(withPet: true);
        await using var context = fixture.CreateContext();
        var service = CreateService(context, authoritativeEnabled: true);
        var session = await service.CreateDailySessionAsync(userId, new()
        {
            ContractVersion = 3,
            PlatformCode = "android",
            CaptureMode = "dual"
        });
        var bootA = Guid.NewGuid();
        var bootB = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await service.SubmitDailyBatchAsync(userId, session.StepSessionId, BuildRequest(
            session.StepSessionId,
            session.Nonce,
            sequence: 1,
            counterSamples:
            [
                Counter(bootA, 1_000_000_000L, 5_000, now.AddSeconds(-20))
            ]));
        Assert.Equal(0, (await fixture.ReadSnapshotAsync(userId)).DailyEligible);

        await service.SubmitDailyBatchAsync(userId, session.StepSessionId, BuildRequest(
            session.StepSessionId,
            session.Nonce,
            sequence: 2,
            counterSamples:
            [
                Counter(bootB, 1_000_000_000L, 30, now.AddSeconds(-10))
            ]));

        var snapshot = await fixture.ReadSnapshotAsync(userId);
        Assert.Equal(0, snapshot.DailyEligible);
        Assert.Equal(0, snapshot.SimpleSegmentCount);
        Assert.Equal(2, await fixture.ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.step_counter_evidence_samples s JOIN dbo.step_sensor_batches b ON b.step_sensor_batch_id = s.batch_id WHERE b.step_session_id = @sessionId",
            new SqlParameter("@sessionId", session.StepSessionId)));
    }

    private async Task<PreparedSegment> PrepareOpenSegmentAsync(
        Guid userId,
        long counterDelta,
        bool authoritativeEnabled)
    {
        await using var context = fixture.CreateContext();
        var service = CreateService(context, authoritativeEnabled);
        var session = await service.CreateDailySessionAsync(userId, new()
        {
            ContractVersion = 3,
            PlatformCode = "android",
            CaptureMode = "dual"
        });
        var bootId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var baselineObservedAt = now.AddSeconds(-70);
        var endpointObservedAt = now.AddSeconds(-50);
        const long startElapsedNs = 1_000_000_000L;
        const long endElapsedNs = 21_000_000_000L;
        const long counterStart = 1_000L;

        await service.SubmitDailyBatchAsync(userId, session.StepSessionId, BuildRequest(
            session.StepSessionId,
            session.Nonce,
            sequence: 1,
            counterSamples:
            [
                Counter(bootId, startElapsedNs, counterStart, baselineObservedAt)
            ]));
        await service.SubmitDailyBatchAsync(userId, session.StepSessionId, BuildRequest(
            session.StepSessionId,
            session.Nonce,
            sequence: 2,
            counterSamples:
            [
                Counter(
                    bootId,
                    endElapsedNs,
                    checked(counterStart + counterDelta),
                    endpointObservedAt)
            ]));

        Assert.Equal(0, (await fixture.ReadSnapshotAsync(userId)).DailyEligible);
        return new(
            userId,
            session.StepSessionId,
            session.Nonce,
            bootId,
            startElapsedNs,
            endElapsedNs,
            baselineObservedAt,
            endpointObservedAt,
            counterDelta);
    }

    private static ValidatedStepService CreateService(
        WalkamonContext context,
        bool authoritativeEnabled,
        IMissionProgressService? missionProgressService = null)
    {
        var options = new StepValidationOptions
        {
            StrictAttestation = false,
            RequirePerBatchAttestation = true,
            CounterSettlementSeconds = 15,
            SimpleStepValidationEnabled = true,
            SimpleStepValidationRevision = SimpleTemporalPolicyBConstants.Revision,
            SimpleStepValidationAuthoritativeEnabled = authoritativeEnabled,
            V3AuthoritativeEnabled = false
        };
        return new(
            context,
            new DevelopmentAttestationVerifier("Development"),
            new AchievementProgressService(context),
            missionProgressService ?? new MissionProgressService(context),
            Options.Create(options),
            Options.Create(new MotionValidationOptions()));
    }

    private static SubmitPvpStepBatchRequest BuildRequest(
        PreparedSegment segment,
        int sequence,
        IReadOnlyList<StepMotionWindowRequest>? motionWindows = null) =>
        BuildRequest(
            segment.SessionId,
            segment.Nonce,
            sequence,
            motionWindows: motionWindows);

    private static SubmitPvpStepBatchRequest BuildRequest(
        Guid sessionId,
        string nonce,
        int sequence,
        IReadOnlyList<StepCounterSampleRequest>? counterSamples = null,
        IReadOnlyList<StepMotionWindowRequest>? motionWindows = null)
    {
        var request = new SubmitPvpStepBatchRequest
        {
            ContractVersion = 3,
            Sequence = sequence,
            Nonce = nonce,
            CounterSamples = counterSamples?.ToList() ?? [],
            MotionWindows = motionWindows?.ToList() ?? []
        };
        request.PayloadHash = StepSensorCanonicalizer.ComputeV3Hash(
            sessionId,
            sequence,
            nonce,
            "dual",
            request.DetectorEvents,
            request.CounterSamples,
            request.MotionWindows);
        request.AttestationToken = $"DEV_BYPASS:{request.PayloadHash}";
        return request;
    }

    private static StepCounterSampleRequest Counter(
        Guid bootId,
        long elapsedNs,
        long total,
        DateTime observedAt) => new()
    {
        ClientSampleId = Guid.NewGuid(),
        BootSessionId = bootId,
        SensorElapsedRealtimeNs = elapsedNs,
        ObservedAt = observedAt,
        CounterTotal = total
    };

    private static StepMotionWindowRequest HardShake(PreparedSegment segment, int startSecond)
    {
        var startedAt = segment.BaselineObservedAt.AddSeconds(startSecond);
        return new()
        {
            BootSessionId = segment.BootSessionId,
            WindowStartElapsedRealtimeNs =
                segment.SegmentStartElapsedNs + startSecond * 1_000_000_000L,
            WindowEndElapsedRealtimeNs =
                segment.SegmentStartElapsedNs + (startSecond + 1L) * 1_000_000_000L,
            WindowStartedAt = startedAt,
            WindowEndedAt = startedAt.AddSeconds(1),
            SampleCount = 25,
            AccelerometerSource = "linear",
            GyroscopeAvailable = true,
            ActivityAvailable = true,
            AccelerationRmsMilli = 20_000,
            AccelerationPeakMilli = 30_000,
            JerkRmsMilli = 40_000,
            GyroscopeRmsMilli = 4_000,
            GyroscopePeakMilli = 8_000,
            AngularTravelMilliDegrees = 130_000,
            DominantFrequencyMilliHz = 2_000,
            PeriodicityBps = 8_000,
            GaitCycleCount = 0,
            ActivityCode = "still",
            ActivityConfidence = 95
        };
    }

    private sealed class ThrowingMissionProgressService : IMissionProgressService
    {
        public Task AddProgressAsync(Guid userId, string metricCode, int amount) =>
            throw new InjectedGameplayFailureException();

        public Task SetProgressMaxAsync(Guid userId, string metricCode, int value) =>
            throw new InjectedGameplayFailureException();

        public Task<bool> ArePrerequisitesMetAsync(Guid userId, Guid missionId) =>
            Task.FromResult(true);
    }

    private sealed class InjectedGameplayFailureException : Exception;

    private sealed record PreparedSegment(
        Guid UserId,
        Guid SessionId,
        string Nonce,
        Guid BootSessionId,
        long SegmentStartElapsedNs,
        long SegmentEndElapsedNs,
        DateTime BaselineObservedAt,
        DateTime EndpointObservedAt,
        long CounterDelta);
}

public sealed record SimpleAuthoritativeDbSnapshot(
    int DailySteps,
    int DailyEligible,
    int SimpleSegmentCount,
    int SimpleSegmentRawSteps,
    int SimpleSegmentEligibleSteps,
    int MissionProgress,
    int AchievementProgress,
    int PetExperience,
    int PetLifeForce,
    int WalletBalance,
    int InventoryQuantity,
    int PvpEventCount);

public sealed class SimpleAuthoritativeSqlFixture : IAsyncLifetime
{
    private static readonly string MasterConnectionString =
        Environment.GetEnvironmentVariable("WALKAMON_TEST_SQL_MASTER")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true";
    private readonly string _databaseName = $"WalkamonSimpleAuthority_{Guid.NewGuid():N}";

    public Guid MissionId { get; } = Guid.NewGuid();
    public Guid AchievementId { get; } = Guid.NewGuid();
    public Guid RewardPackageId { get; } = Guid.NewGuid();
    public string ConnectionString
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(MasterConnectionString)
            {
                InitialCatalog = _databaseName
            };
            return builder.ConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        var schema = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Sql", "WalkamonFreshSchema.sql"));
        schema = schema
            .Replace(
                "CREATE DATABASE Walkamon;",
                $"CREATE DATABASE [{_databaseName}];",
                StringComparison.Ordinal)
            .Replace(
                "USE Walkamon;",
                $"USE [{_databaseName}];",
                StringComparison.Ordinal);
        await using (var master = new SqlConnection(MasterConnectionString))
        {
            await master.OpenAsync();
            await ExecuteBatchesAsync(master, schema);
        }

        var upgrade = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Sql", "pvp_sprint_upgrade.sql"));
        await using (var database = new SqlConnection(ConnectionString))
        {
            await database.OpenAsync();
            await ExecuteBatchesAsync(database, upgrade);
        }

        await ExecuteAsync(
            """
            MERGE dbo.system_settings AS target
            USING (VALUES ('StepToExpRate', '1')) AS source(setting_key, setting_value)
            ON target.setting_key = source.setting_key
            WHEN MATCHED THEN
                UPDATE SET setting_value = source.setting_value, updated_at = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (setting_key, setting_value) VALUES (source.setting_key, source.setting_value);

            INSERT dbo.reward_packages (reward_package_id, package_name, wallet_amount)
            VALUES (@rewardPackageId, N'Simple authority verification reward', 7);

            INSERT dbo.missions
                (mission_id, mission_type_code, title, metric_code, target_value,
                 reward_package_id, is_cancelable, is_active)
            VALUES
                (@missionId, 'overall', N'Simple authority verification mission',
                 'steps', 1000, @rewardPackageId, 0, 1);

            INSERT dbo.achievements
                (achievement_id, title, metric_code, target_value,
                 reward_package_id, is_active)
            VALUES
                (@achievementId, N'Simple authority verification achievement',
                 'steps', 1000, @rewardPackageId, 1);
            """,
            new SqlParameter("@rewardPackageId", RewardPackageId),
            new SqlParameter("@missionId", MissionId),
            new SqlParameter("@achievementId", AchievementId));
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{_databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_databaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
    }

    public WalkamonContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WalkamonContext>()
            .UseSqlServer(
                ConnectionString,
                sql => sql.EnableRetryOnFailure(5, TimeSpan.FromMilliseconds(100), null))
            .Options;
        return new WalkamonContext(options, new HttpContextAccessor());
    }

    public async Task<Guid> SeedUserAsync(bool withPet)
    {
        var userId = Guid.NewGuid();
        var petId = Guid.NewGuid();
        await ExecuteAsync(
            """
            DECLARE @roleId INT =
                (SELECT role_id FROM dbo.roles WHERE role_code = '0');

            INSERT dbo.users
                (user_id, role_id, email, normalized_email, password_hash,
                 email_confirmed, status_code, created_at, updated_at)
            VALUES
                (@userId, @roleId, @email, UPPER(@email), 'LOCAL-TEST-NOT-A-LOGIN',
                 1, 'active', SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT dbo.user_profiles
                (user_id, username, language_code, theme_code, time_zone_id,
                 show_activity_stats, notifications_enabled)
            VALUES
                (@userId, @username, 'vi-VN', 'light', 'Asia/Ho_Chi_Minh', 1, 1);

            INSERT dbo.wallets (user_id, balance) VALUES (@userId, 0);

            IF @withPet = 1
            BEGIN
                INSERT dbo.pets
                    (pet_id, pet_name, life_force, energy, bond, exp,
                     life_force_rate, energy_rate, bond_rate, exp_rate)
                VALUES
                    (@petId, N'Local Authority Pet', 100, 100, 100, 100, 1, 1, 1, 1);

                INSERT dbo.user_pets
                    (user_id, pet_id, level, pet_name, pet_exp, pet_energy, pet_bond,
                     pet_life_force, current_pet_exp, current_pet_energy, current_pet_bond,
                     current_pet_life_force, energy_updated_at, bond_updated_at,
                     life_force_updated_at, exp_updated_at)
                VALUES
                    (@userId, @petId, 1, N'Local Authority Pet', 100, 100, 100, 100,
                     0, 100, 100, 100, SYSUTCDATETIME(), SYSUTCDATETIME(),
                     SYSUTCDATETIME(), SYSUTCDATETIME());
            END
            """,
            new SqlParameter("@userId", userId),
            new SqlParameter("@petId", petId),
            new SqlParameter("@email", $"simple-authority-{userId:N}@local.invalid"),
            new SqlParameter("@username", $"simple-{userId:N}"[..30]),
            new SqlParameter("@withPet", withPet));
        return userId;
    }

    public Task AgeEvidenceAsync(Guid sessionId, int latestEvidenceSequence) =>
        ExecuteAsync(
            """
            UPDATE dbo.step_sensor_batches
            SET received_at = CASE sequence
                WHEN 1 THEN DATEADD(SECOND, -65, SYSUTCDATETIME())
                WHEN 2 THEN DATEADD(SECOND, -45, SYSUTCDATETIME())
                ELSE DATEADD(SECOND, -40, SYSUTCDATETIME())
            END
            WHERE step_session_id = @sessionId
              AND sequence <= @latestEvidenceSequence;
            """,
            new SqlParameter("@sessionId", sessionId),
            new SqlParameter("@latestEvidenceSequence", latestEvidenceSequence));

    public async Task<SimpleAuthoritativeDbSnapshot> ReadSnapshotAsync(Guid userId) => new(
        await ScalarAsync<int>(
            "SELECT COALESCE(SUM(step_count), 0) FROM dbo.daily_steps WHERE user_id = @userId",
            new SqlParameter("@userId", userId)),
        await ScalarAsync<int>(
            "SELECT COALESCE(SUM(eligible_step_count), 0) FROM dbo.daily_steps WHERE user_id = @userId",
            new SqlParameter("@userId", userId)),
        await ScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.validated_step_records WHERE user_id = @userId AND source_code = 'simple_counter_segment'",
            new SqlParameter("@userId", userId)),
        await ScalarAsync<int>(
            "SELECT COALESCE(SUM(step_count), 0) FROM dbo.validated_step_records WHERE user_id = @userId AND source_code = 'simple_counter_segment'",
            new SqlParameter("@userId", userId)),
        await ScalarAsync<int>(
            "SELECT COALESCE(SUM(eligible_step_count), 0) FROM dbo.validated_step_records WHERE user_id = @userId AND source_code = 'simple_counter_segment'",
            new SqlParameter("@userId", userId)),
        await ScalarAsync<int>(
            "SELECT COALESCE(progress_value, 0) FROM dbo.user_missions WHERE user_id = @userId AND mission_id = @missionId",
            new SqlParameter("@userId", userId),
            new SqlParameter("@missionId", MissionId)),
        await ScalarAsync<int>(
            "SELECT COALESCE(progress_value, 0) FROM dbo.user_achievements WHERE user_id = @userId AND achievement_id = @achievementId",
            new SqlParameter("@userId", userId),
            new SqlParameter("@achievementId", AchievementId)),
        await ScalarAsync<int>(
            "SELECT COALESCE(current_pet_exp, 0) FROM dbo.user_pets WHERE user_id = @userId",
            new SqlParameter("@userId", userId)),
        await ScalarAsync<int>(
            "SELECT COALESCE(current_pet_life_force, 0) FROM dbo.user_pets WHERE user_id = @userId",
            new SqlParameter("@userId", userId)),
        await ScalarAsync<int>(
            "SELECT COALESCE(balance, 0) FROM dbo.wallets WHERE user_id = @userId",
            new SqlParameter("@userId", userId)),
        await ScalarAsync<int>(
            "SELECT COALESCE(SUM(quantity), 0) FROM dbo.inventory_items WHERE user_id = @userId",
            new SqlParameter("@userId", userId)),
        await ScalarAsync<int>("SELECT COUNT(*) FROM dbo.pvp_match_events"));

    public async Task<T> ScalarAsync<T>(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        if (value is null or DBNull) return default!;
        return (T)Convert.ChangeType(value, typeof(T));
    }

    public async Task ExecuteAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteBatchesAsync(SqlConnection connection, string script)
    {
        foreach (var batch in Regex.Split(
                     script,
                     @"^\s*GO\s*;?\s*$",
                     RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync();
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SimpleAuthoritativeSqlCollection :
    ICollectionFixture<SimpleAuthoritativeSqlFixture>
{
    public const string Name = "Simple authoritative SQL integration";
}
