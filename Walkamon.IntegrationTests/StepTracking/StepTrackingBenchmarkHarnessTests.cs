using System.Text.Json;
using BLL.Options;
using BLL.Service;
using DAL.Models;
using Xunit;

namespace Walkamon.IntegrationTests.StepTracking;

public sealed class StepTrackingBenchmarkHarnessTests
{
    private static readonly Guid SessionId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid BootId =
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly DateTime StartedAt =
        new(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BenchmarkFlagDefaultsOffAndNoOpSinkCreatesNoArtifact()
    {
        var directory = TempDirectory();
        try
        {
            Assert.False(new StepTrackingBenchmarkOptions().Enabled);
            Assert.Same(
                NullStepTrackingBenchmarkSink.Instance,
                StepTrackingBenchmarkSinkFactory.Create(
                    isDevelopment: true,
                    new StepTrackingBenchmarkOptions { Enabled = false }));
            Assert.Same(
                NullStepTrackingBenchmarkSink.Instance,
                StepTrackingBenchmarkSinkFactory.Create(
                    isDevelopment: false,
                    new StepTrackingBenchmarkOptions
                    {
                        Enabled = true,
                        ArtifactDirectory = directory
                    }));
            await NullStepTrackingBenchmarkSink.Instance.RecordShadowIntervalAsync(
                Session(),
                Assessment());
            Assert.False(Directory.Exists(directory));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task EnabledFileSinkWritesSettledShadowInterval()
    {
        var directory = TempDirectory();
        try
        {
            var store = new StepTrackingBenchmarkArtifactStore(directory);
            await AddTrialAsync(store, "normal-hand-01", 100);
            var sink = new FileStepTrackingBenchmarkSink(new()
            {
                Enabled = true,
                ArtifactDirectory = directory
            });

            await sink.RecordShadowIntervalAsync(Session(), Assessment());

            var record = Assert.Single(store.ReadTyped<StepTrackingBenchmarkShadowInterval>(
                StepTrackingBenchmarkRecordTypes.ShadowInterval));
            Assert.Equal("normal-hand-01", record.TrialId);
            Assert.Equal(CounterRecoveryShadowLabels.MotionSupportPresent,
                record.ShadowAssessment);
            Assert.Equal(0, record.AuthoritativeSteps);
            Assert.Equal(0, record.RewardDelta);
            Assert.Equal(0, record.ExpDelta);
            Assert.Equal(0, record.PvpDelta);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task SameEvidenceFingerprintIsDedupedAcrossRetries()
    {
        var directory = TempDirectory();
        try
        {
            var store = new StepTrackingBenchmarkArtifactStore(directory);
            await AddTrialAsync(store, "normal-hand-01", 100);
            var sink = new FileStepTrackingBenchmarkSink(new()
            {
                Enabled = true,
                ArtifactDirectory = directory
            });
            var assessment = Assessment();

            await sink.RecordShadowIntervalAsync(Session(), assessment);
            await sink.RecordShadowIntervalAsync(Session(), assessment);

            Assert.Single(store.ReadTyped<StepTrackingBenchmarkShadowInterval>(
                StepTrackingBenchmarkRecordTypes.ShadowInterval));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void NormalTrialProducesOnlyDescriptiveMetrics()
    {
        var row = BuildSummary(
            trialId: "normal-hand-01",
            groundTruth: 100,
            detectorPersisted: 61,
            counterDelta: 85,
            detectorAccepted: 55,
            detectorSuspicious: 6,
            detectorRejected: 0,
            shadowAssessment: CounterRecoveryShadowLabels.MotionSupportPresent);

        Assert.Equal(0.61m, row.DetectorRecallVsGroundTruth);
        Assert.Equal(-15, row.CounterError);
        Assert.Equal(15, row.CounterAbsoluteError);
        Assert.Equal(-15m, row.CounterErrorPercent);
        Assert.Equal(0.55m, row.FinalDetectorAcceptedVsGroundTruth);
        AssertNoRecoveryFormula(row);
    }

    [Fact]
    public void SlowTrialPreservesPositiveCounterError()
    {
        var row = BuildSummary(
            trialId: "slow-hand-01",
            groundTruth: 100,
            detectorPersisted: 77,
            counterDelta: 108,
            detectorAccepted: 66,
            detectorSuspicious: 11,
            detectorRejected: 0,
            shadowAssessment: CounterRecoveryShadowLabels.MotionSupportPresent);

        Assert.Equal(8, row.CounterError);
        Assert.Equal(8, row.CounterAbsoluteError);
        Assert.Equal(8m, row.CounterErrorPercent);
    }

    [Fact]
    public void ShakeTrialAvoidsDivisionByZeroAndExportsFalseCounts()
    {
        var row = BuildSummary(
            trialId: "shake-hard-01",
            groundTruth: 0,
            detectorPersisted: 100,
            counterDelta: 119,
            detectorAccepted: 0,
            detectorSuspicious: 7,
            detectorRejected: 93,
            shadowAssessment: CounterRecoveryShadowLabels.BlockedHardShake);

        Assert.Null(row.DetectorRecallVsGroundTruth);
        Assert.Null(row.CounterError);
        Assert.Null(row.CounterAbsoluteError);
        Assert.Null(row.CounterErrorPercent);
        Assert.Null(row.FinalDetectorAcceptedVsGroundTruth);
        Assert.Equal(100, row.FalseDetectorCount);
        Assert.Equal(119, row.FalseCounterCount);
        Assert.Equal(0, row.FalseAcceptedCount);
        Assert.Equal(0, row.AuthoritativeSteps);
    }

    [Fact]
    public void ExcessVsAcceptedDetectorIsDiagnosticOnly()
    {
        var shake = BuildSummary(
            trialId: "shake-hard-01",
            groundTruth: 0,
            detectorPersisted: 100,
            counterDelta: 119,
            detectorAccepted: 0,
            detectorSuspicious: 7,
            detectorRejected: 93,
            shadowAssessment: CounterRecoveryShadowLabels.BlockedHardShake);

        Assert.Equal(119, shake.ExcessVsAcceptedDetector);
        Assert.Equal(CounterRecoveryShadowLabels.BlockedHardShake,
            shake.ShadowAssessment);
        Assert.Equal(0, shake.AuthoritativeSteps);
        AssertNoRecoveryFormula(shake);
    }

    [Fact]
    public async Task ArtifactContainsNoAuthenticationOrAttestationSecrets()
    {
        var directory = TempDirectory();
        try
        {
            var store = new StepTrackingBenchmarkArtifactStore(directory);
            await AddTrialAsync(store, "normal-hand-01", 100);
            var sink = new FileStepTrackingBenchmarkSink(new()
            {
                Enabled = true,
                ArtifactDirectory = directory
            });
            await sink.RecordShadowIntervalAsync(Session(), Assessment());

            var artifact = await File.ReadAllTextAsync(store.JsonlPath);
            Assert.DoesNotContain("jwt", artifact, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("authorization", artifact, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("integrityToken", artifact, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("attestationToken", artifact, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("nonce", artifact, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("recoveredSteps", artifact, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CsvHasOneDedupedRowPerPhysicalTrial()
    {
        var trial = Trial("normal-hand-01", 100);
        var interval = Interval("normal-hand-01", "fingerprint-a") with
        {
            RecordedAtUtc = StartedAt.AddSeconds(10),
            DetectorCount = 50
        };
        var updated = interval with
        {
            DedupeKey = "shadow_interval:fingerprint-b",
            RecordedAtUtc = StartedAt.AddSeconds(20),
            IntervalEndElapsedNs = 30,
            CounterEnd = 1_090,
            EvidenceFingerprint = "fingerprint-b",
            DetectorCount = 61
        };

        var rows = StepTrackingBenchmarkSummaryBuilder.Build(
            [trial],
            [interval, updated],
            [Finalization("normal-hand-01", 61, 85, 55, 6, 0)]);

        var row = Assert.Single(rows);
        Assert.Equal(61, row.DetectorPersisted);
        Assert.Equal(10, row.MotionWindowCount);
        Assert.Equal("fingerprint-b", row.EvidenceFingerprint);
    }

    private static StepTrackingBenchmarkSummaryRow BuildSummary(
        string trialId,
        int groundTruth,
        int detectorPersisted,
        int counterDelta,
        int detectorAccepted,
        int detectorSuspicious,
        int detectorRejected,
        string shadowAssessment)
    {
        var interval = Interval(trialId, $"fingerprint-{trialId}") with
        {
            CounterDelta = counterDelta,
            CounterEnd = 1_000 + counterDelta,
            DetectorCount = detectorPersisted,
            DetectorAccepted = detectorAccepted,
            DetectorSuspicious = detectorSuspicious,
            DetectorRejected = detectorRejected,
            CounterExcess = Math.Max(0, counterDelta - detectorPersisted),
            ExcessVsAcceptedDetector = Math.Max(0, counterDelta - detectorAccepted),
            ShadowAssessment = shadowAssessment
        };
        return Assert.Single(StepTrackingBenchmarkSummaryBuilder.Build(
            [Trial(trialId, groundTruth)],
            [interval],
            [Finalization(
                trialId,
                detectorPersisted,
                counterDelta,
                detectorAccepted,
                detectorSuspicious,
                detectorRejected)]));
    }

    private static StepTrackingBenchmarkTrialMetadata Trial(
        string trialId,
        int groundTruth) => new(
        StepTrackingBenchmarkRecordTypes.TrialMetadata,
        1,
        $"trial_metadata:{trialId}:{SessionId:D}",
        StartedAt,
        trialId,
        SessionId,
        trialId.StartsWith("shake", StringComparison.Ordinal) ? "SHAKE_HARD" : "WALK",
        groundTruth,
        "HAND",
        "ON",
        trialId.StartsWith("slow", StringComparison.Ordinal) ? "SLOW" : "NORMAL",
        "SM-A175F",
        StartedAt);

    private static StepTrackingBenchmarkShadowInterval Interval(
        string trialId,
        string fingerprint) => new(
        StepTrackingBenchmarkRecordTypes.ShadowInterval,
        1,
        $"shadow_interval:{fingerprint}",
        StartedAt.AddSeconds(10),
        trialId,
        SessionId,
        BootId,
        StartedAt,
        10,
        20,
        1_000,
        1_085,
        85,
        61,
        55,
        6,
        0,
        0,
        24,
        30,
        10,
        10,
        0,
        0,
        0,
        false,
        [new("walking", 10, 90, 100, 95)],
        [new("accepted", 55), new("unavailable", 6)],
        CounterRecoveryShadowLabels.MotionSupportPresent,
        "interval-a",
        fingerprint,
        0,
        0,
        0,
        0);

    private static StepTrackingBenchmarkTrialFinalization Finalization(
        string trialId,
        int detectorPersisted,
        int counterDelta,
        int detectorAccepted,
        int detectorSuspicious,
        int detectorRejected) => new(
        StepTrackingBenchmarkRecordTypes.TrialFinalization,
        1,
        $"trial_finalization:{trialId}",
        StartedAt.AddMinutes(2),
        trialId,
        SessionId,
        [BootId],
        StartedAt.AddMinutes(2),
        120,
        detectorPersisted,
        detectorPersisted,
        detectorPersisted,
        0,
        1_000,
        1_000 + counterDelta,
        counterDelta,
        Math.Max(0, counterDelta - detectorPersisted),
        Math.Max(0, counterDelta - detectorAccepted),
        detectorAccepted,
        detectorSuspicious,
        detectorRejected,
        0,
        10,
        10,
        0,
        0,
        0,
        0,
        false,
        [new("WALKING", 10, 90, 100, 95)],
        0,
        0,
        0,
        0,
        $"final-{trialId}");

    private static PvpStepSession Session() => new()
    {
        StepSessionId = SessionId,
        CreatedAt = StartedAt,
        SensorModeCode = "dual",
        ContractVersion = 3
    };

    private static CounterRecoveryShadowAssessment Assessment() => new(
        SessionId,
        BootId,
        10,
        20,
        1_000,
        1_085,
        85,
        61,
        61,
        55,
        6,
        0,
        0,
        24,
        10,
        10,
        0,
        0,
        0,
        false,
        [new("walking", 10, 90, 100, 95)],
        [new("accepted", 55), new("unavailable", 6)],
        CounterRecoveryShadowLabels.MotionSupportPresent,
        24,
        "interval-a",
        "fingerprint-a");

    private static async Task AddTrialAsync(
        StepTrackingBenchmarkArtifactStore store,
        string trialId,
        int groundTruth)
    {
        var trial = Trial(trialId, groundTruth);
        await store.AppendIfNewAsync(trial, trial.DedupeKey);
    }

    private static void AssertNoRecoveryFormula(object value)
    {
        Assert.DoesNotContain(value.GetType().GetProperties(), property =>
            property.Name.Contains("RecoveredSteps", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("RecoveryScore", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("RecoveryProbability", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("RecommendedRecovered", StringComparison.OrdinalIgnoreCase));
    }

    private static string TempDirectory() => Path.Combine(
        Path.GetTempPath(),
        "walkamon-step-benchmark-tests",
        Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
