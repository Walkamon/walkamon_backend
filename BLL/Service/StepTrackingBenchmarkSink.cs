using BLL.Options;
using DAL.Models;

namespace BLL.Service;

public interface IStepTrackingBenchmarkSink
{
    Task RecordShadowIntervalAsync(
        PvpStepSession session,
        CounterRecoveryShadowAssessment assessment,
        CancellationToken cancellationToken = default);

    Task RecordSimpleShadowIntervalAsync(
        PvpStepSession session,
        SimpleStepIntervalAssessment assessment,
        bool v3AuthoritativeEnabled,
        CancellationToken cancellationToken = default);

    Task RecordSimpleTemporalShadowIntervalAsync(
        PvpStepSession session,
        TemporalFraudEvaluation evaluation,
        CancellationToken cancellationToken = default);
}

public sealed class NullStepTrackingBenchmarkSink : IStepTrackingBenchmarkSink
{
    public static readonly NullStepTrackingBenchmarkSink Instance = new();

    private NullStepTrackingBenchmarkSink()
    {
    }

    public Task RecordShadowIntervalAsync(
        PvpStepSession session,
        CounterRecoveryShadowAssessment assessment,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RecordSimpleShadowIntervalAsync(
        PvpStepSession session,
        SimpleStepIntervalAssessment assessment,
        bool v3AuthoritativeEnabled,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RecordSimpleTemporalShadowIntervalAsync(
        PvpStepSession session,
        TemporalFraudEvaluation evaluation,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public static class StepTrackingBenchmarkSinkFactory
{
    public static IStepTrackingBenchmarkSink Create(
        bool isDevelopment,
        StepTrackingBenchmarkOptions options) =>
        isDevelopment && options.Enabled
            ? new FileStepTrackingBenchmarkSink(options)
            : NullStepTrackingBenchmarkSink.Instance;
}

public sealed class FileStepTrackingBenchmarkSink : IStepTrackingBenchmarkSink
{
    private readonly StepTrackingBenchmarkArtifactStore _store;

    public FileStepTrackingBenchmarkSink(StepTrackingBenchmarkOptions options)
    {
        _store = new StepTrackingBenchmarkArtifactStore(
            options.ArtifactDirectory,
            options.JsonlFileName);
    }

    public async Task RecordShadowIntervalAsync(
        PvpStepSession session,
        CounterRecoveryShadowAssessment assessment,
        CancellationToken cancellationToken = default)
    {
        var trial = _store.FindTrial(session.StepSessionId);
        var trialId = trial?.TrialId ?? $"unmapped-{session.StepSessionId:N}";
        var dedupeKey = $"shadow_interval:{assessment.EvidenceFingerprint}";
        var record = new StepTrackingBenchmarkShadowInterval(
            StepTrackingBenchmarkRecordTypes.ShadowInterval,
            1,
            dedupeKey,
            AsUtc(session.LastSubmittedAt ?? session.CreatedAt),
            trialId,
            assessment.SessionId,
            assessment.BootSessionId,
            AsUtc(session.CreatedAt),
            assessment.IntervalStartElapsedNs,
            assessment.IntervalEndElapsedNs,
            assessment.CounterFrom,
            assessment.CounterTo,
            assessment.CounterDelta,
            assessment.DetectorCount,
            assessment.DetectorAcceptedCount,
            assessment.DetectorSuspiciousCount,
            assessment.DetectorRejectedCount,
            assessment.DetectorPendingCount,
            assessment.CounterExcess,
            Math.Max(0, assessment.CounterDelta - assessment.DetectorAcceptedCount),
            assessment.MotionWindowCount,
            assessment.MotionAcceptedWindowCount,
            assessment.MotionSuspiciousWindowCount,
            assessment.MotionRejectedWindowCount,
            assessment.MotionUnavailableWindowCount,
            assessment.HardShakeMajority,
            assessment.ActivityDistribution,
            assessment.GaitDistribution,
            assessment.ShadowAssessment,
            assessment.ShadowIntervalId,
            assessment.EvidenceFingerprint,
            AuthoritativeSteps: 0,
            RewardDelta: 0,
            ExpDelta: 0,
            PvpDelta: 0);
        await _store.AppendIfNewAsync(record, dedupeKey, cancellationToken);
    }

    public async Task RecordSimpleShadowIntervalAsync(
        PvpStepSession session,
        SimpleStepIntervalAssessment assessment,
        bool v3AuthoritativeEnabled,
        CancellationToken cancellationToken = default)
    {
        var trial = _store.FindTrial(session.StepSessionId);
        var trialId = trial?.TrialId ?? $"unmapped-{session.StepSessionId:N}";
        var dedupeKey = $"simple_shadow_interval:{assessment.EvidenceFingerprint}";
        var record = new StepTrackingBenchmarkSimpleShadowInterval(
            StepTrackingBenchmarkRecordTypes.SimpleShadowInterval,
            1,
            dedupeKey,
            AsUtc(session.LastSubmittedAt ?? session.CreatedAt),
            trialId,
            assessment.SessionId,
            assessment.BootSessionId,
            assessment.StartClientSampleId,
            assessment.EndClientSampleId,
            assessment.IntervalStartElapsedNs,
            assessment.IntervalEndElapsedNs,
            assessment.CounterStart,
            assessment.CounterEnd,
            assessment.CounterDelta,
            assessment.DetectorCount,
            assessment.MotionWindowCount,
            assessment.MotionAccepted,
            assessment.MotionSuspicious,
            assessment.MotionRejected,
            assessment.MotionUnavailable,
            assessment.HardShakeBatchCount,
            assessment.HardShakeObserved,
            assessment.ActivityDistribution,
            assessment.SimpleDecision,
            assessment.ShadowSimpleSteps,
            assessment.ReasonCodes,
            assessment.SimpleIntervalId,
            assessment.EvidenceFingerprint,
            v3AuthoritativeEnabled,
            AuthoritativeSteps: 0,
            RewardDelta: 0,
            ExpDelta: 0,
            PvpDelta: 0);
        await _store.AppendIfNewAsync(record, dedupeKey, cancellationToken);
    }

    public async Task RecordSimpleTemporalShadowIntervalAsync(
        PvpStepSession session,
        TemporalFraudEvaluation evaluation,
        CancellationToken cancellationToken = default)
    {
        var trial = _store.FindTrial(session.StepSessionId);
        var trialId = trial?.TrialId ?? $"unmapped-{session.StepSessionId:N}";
        var dedupeKey = $"simple_temporal_shadow_interval:{evaluation.EvidenceFingerprint}";
        var record = new StepTrackingBenchmarkSimpleTemporalShadowInterval(
            StepTrackingBenchmarkRecordTypes.SimpleTemporalShadowInterval,
            1,
            dedupeKey,
            AsUtc(session.LastSubmittedAt ?? session.CreatedAt),
            trialId,
            evaluation.SessionId,
            evaluation.BootSessionId,
            evaluation.CounterIntervalId,
            evaluation.IntervalStartElapsedNs,
            evaluation.IntervalEndElapsedNs,
            evaluation.CounterDelta,
            evaluation.DetectorCount,
            evaluation.MotionWindowCount,
            evaluation.MotionAccepted,
            evaluation.MotionSuspicious,
            evaluation.MotionRejected,
            evaluation.MotionUnavailable,
            evaluation.FraudRegionCount,
            evaluation.FraudDurationMs,
            evaluation.IntervalDurationMs,
            evaluation.FraudCoverageRatio,
            evaluation.HardShakeRegionCount,
            evaluation.MaxFraudRegionDurationMs,
            evaluation.ShadowCounterCandidate,
            evaluation.ActivityDistribution,
            evaluation.FraudRegions,
            evaluation.SimpleV2EvidenceClass,
            evaluation.EvidenceFingerprint,
            Authoritative: false,
            AuthoritativeSteps: 0,
            RewardDelta: 0,
            ExpDelta: 0,
            PvpDelta: 0);
        await _store.AppendIfNewAsync(record, dedupeKey, cancellationToken);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
