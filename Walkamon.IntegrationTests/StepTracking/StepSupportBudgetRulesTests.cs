using BLL.Service;

namespace Walkamon.IntegrationTests.StepTracking;

public sealed class StepSupportBudgetRulesTests
{
    [Fact]
    public void DetectorAheadRemainsPendingUntilSettlementCloses()
    {
        var decision = StepReconciliationRules.Evaluate(
            detectorSteps: 10,
            counterDelta: 6,
            settlementClosed: false);

        Assert.Equal("pending_reconciliation", decision.Status);
        Assert.Equal(10, decision.PendingDetectorSteps);
        Assert.Equal(0, decision.SupportBudget);
    }

    [Fact]
    public void CounterCatchUpClosesReconciliationWithoutWaitingForTimeout()
    {
        var decision = StepReconciliationRules.Evaluate(
            detectorSteps: 10,
            counterDelta: 10,
            settlementClosed: false);

        Assert.Equal("accepted", decision.Status);
        Assert.Equal(10, decision.SupportBudget);
        Assert.Equal(0, decision.CounterExcessSteps);
    }

    [Fact]
    public void MotionRejectedCandidatesDoNotConsumeSupportBudget()
    {
        var candidates = Enumerable.Range(1, 10)
            .Select(index => Candidate(
                index,
                motionStatus: index <= 4 ? "rejected" : "accepted"))
            .ToArray();

        var allocation = StepSupportBudgetRules.Allocate(6, candidates);

        Assert.Equal(6, allocation.CandidatesToAccept.Count);
        Assert.Equal(4, allocation.FinalResolutions.Count(x => x.Status == "rejected"));
        Assert.DoesNotContain(
            allocation.FinalResolutions,
            x => x.Reason == "counter_support_budget_exhausted");
        Assert.Equal(6, allocation.ConsumedSupportBudget);
        Assert.Equal(0, allocation.RemainingSupportBudget);
    }

    [Fact]
    public void MotionValidCandidatesConsumeBudgetChronologically()
    {
        var candidates = Enumerable.Range(1, 8)
            .Reverse()
            .Select(index => Candidate(index, motionStatus: "accepted"))
            .ToArray();
        var expectedAccepted = candidates
            .OrderBy(x => x.SensorElapsedRealtimeNs)
            .Take(6)
            .Select(x => x.RecordId)
            .ToArray();

        var allocation = StepSupportBudgetRules.Allocate(6, candidates);

        Assert.Equal(expectedAccepted, allocation.CandidatesToAccept);
        Assert.Equal(2, allocation.FinalResolutions.Count(x =>
            x.Status == "suspicious" &&
            x.Reason == "counter_support_budget_exhausted"));
    }

    [Fact]
    public void MissingMotionRemainsPendingAndDoesNotConsumeBudget()
    {
        var candidates = new[]
        {
            Candidate(1, motionStatus: "rejected"),
            Candidate(2, motionStatus: "suspicious"),
            Candidate(3, motionStatus: "unavailable"),
            Candidate(4, motionStatus: "unknown"),
            Candidate(5, motionStatus: "accepted")
        };

        var allocation = StepSupportBudgetRules.Allocate(1, candidates);

        Assert.Single(allocation.CandidatesToAccept);
        Assert.Equal(candidates[4].RecordId, allocation.CandidatesToAccept[0]);
        Assert.Contains(allocation.FinalResolutions, x =>
            x.Status == "rejected" &&
            x.Reason == "motion_rejected_after_reconciliation");
        Assert.Contains(allocation.FinalResolutions, x =>
            x.Reason == "motion_suspicious_after_reconciliation");
        Assert.Equal(2, allocation.FinalResolutions.Count(x =>
            x.Status == "pending" &&
            x.Reason == "pending_motion_evidence"));
        Assert.Equal(1, allocation.ConsumedSupportBudget);
    }

    [Fact]
    public void MissingMotionBecomesSuspiciousOnlyAfterLifecycleCloses()
    {
        var candidate = Candidate(
            1,
            motionStatus: "unavailable",
            motionEvidenceLifecycleClosed: true);

        var allocation = StepSupportBudgetRules.Allocate(1, [candidate]);

        var resolution = Assert.Single(allocation.FinalResolutions);
        Assert.Equal("suspicious", resolution.Status);
        Assert.Equal("motion_unavailable_after_settlement", resolution.Reason);
        Assert.Equal(0, allocation.ConsumedSupportBudget);
        Assert.Equal(1, allocation.RemainingSupportBudget);
    }

    [Fact]
    public void ExistingAcceptedRecordsConsumeBudgetOnlyOnce()
    {
        var candidates = new[]
        {
            Candidate(1, validationStatus: "accepted"),
            Candidate(2, validationStatus: "accepted"),
            Candidate(3),
            Candidate(4),
            Candidate(5),
            Candidate(6)
        };

        var firstPass = StepSupportBudgetRules.Allocate(4, candidates);

        Assert.Equal(new[] { candidates[2].RecordId, candidates[3].RecordId },
            firstPass.CandidatesToAccept);
        Assert.Equal(2, firstPass.FinalResolutions.Count(x =>
            x.Reason == "counter_support_budget_exhausted"));

        var retryCandidates = candidates.Select(candidate =>
            firstPass.CandidatesToAccept.Contains(candidate.RecordId)
                ? candidate with { ValidationStatus = "accepted" }
                : firstPass.FinalResolutions.Any(x => x.RecordId == candidate.RecordId)
                    ? candidate with { ValidationStatus = "suspicious" }
                    : candidate).ToArray();
        var retry = StepSupportBudgetRules.Allocate(4, retryCandidates);

        Assert.Empty(retry.CandidatesToAccept);
        Assert.Empty(retry.FinalResolutions);
        Assert.Equal(4, retry.ConsumedSupportBudget);
    }

    [Fact]
    public void CounterOnlyDeltaAfterSettlementCreatesOnlyCounterExcess()
    {
        var decision = StepReconciliationRules.Evaluate(
            detectorSteps: 0,
            counterDelta: 10,
            settlementClosed: true);

        Assert.Equal(0, decision.SupportBudget);
        Assert.Equal(10, decision.CounterExcessSteps);
        Assert.Equal("suspicious", decision.Status);
        Assert.Equal(10, StepSupportBudgetRules.CalculateIncrementalCounterExcess(
            detectorSteps: 0,
            counterDelta: 10,
            existingCounterExcess: 0));
        Assert.Equal(0, StepSupportBudgetRules.CalculateIncrementalCounterExcess(
            detectorSteps: 0,
            counterDelta: 10,
            existingCounterExcess: 10));
    }

    [Fact]
    public void CounterExcessDependsOnDetectorCountNotMotionValidCount()
    {
        var decision = StepReconciliationRules.Evaluate(
            detectorSteps: 10,
            counterDelta: 14,
            settlementClosed: true);

        Assert.Equal(10, decision.SupportBudget);
        Assert.Equal(4, decision.CounterExcessSteps);
        Assert.Equal(4, StepSupportBudgetRules.CalculateIncrementalCounterExcess(
            detectorSteps: 10,
            counterDelta: 14,
            existingCounterExcess: 0));
    }

    [Fact]
    public void HardShakeDatasetProducesZeroAcceptedCandidates()
    {
        var candidates = Enumerable.Range(1, 122)
            .Select(index => Candidate(index, motionStatus: "rejected"))
            .Concat(Enumerable.Range(123, 4)
                .Select(index => Candidate(
                    index,
                    motionStatus: "accepted",
                    hardShakeVeto: true)))
            .ToArray();
        var decision = StepReconciliationRules.Evaluate(
            detectorSteps: 126,
            counterDelta: 140,
            settlementClosed: true);

        var allocation = StepSupportBudgetRules.Allocate(
            decision.SupportBudget,
            candidates);

        Assert.Empty(allocation.CandidatesToAccept);
        Assert.Equal(122, allocation.FinalResolutions.Count(x => x.Status == "rejected"));
        Assert.Equal(4, allocation.FinalResolutions.Count(x =>
            x.Status == "suspicious" && x.Reason == "hard_shake_batch_veto"));
        Assert.Equal(14, decision.CounterExcessSteps);
        Assert.Equal(14, StepSupportBudgetRules.CalculateIncrementalCounterExcess(
            detectorSteps: 126,
            counterDelta: 140,
            existingCounterExcess: 0));
    }

    private static StepSupportCandidate Candidate(
        int index,
        string validationStatus = "pending",
        string motionStatus = "accepted",
        bool hardShakeVeto = false,
        bool motionEvidenceLifecycleClosed = false) => new(
            RecordId: DeterministicGuid(index),
            ClientEventId: DeterministicGuid(index + 1000),
            SensorElapsedRealtimeNs: index * 1_000_000L,
            StepCount: 1,
            ValidationStatus: validationStatus,
            MotionStatus: motionStatus,
            HardShakeVeto: hardShakeVeto,
            MotionEvidenceLifecycleClosed: motionEvidenceLifecycleClosed);

    private static Guid DeterministicGuid(int value)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(value).CopyTo(bytes, 0);
        return new Guid(bytes);
    }
}
