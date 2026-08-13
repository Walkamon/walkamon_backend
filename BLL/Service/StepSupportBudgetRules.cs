namespace BLL.Service;

public sealed record StepSupportCandidate(
    Guid RecordId,
    Guid ClientEventId,
    long SensorElapsedRealtimeNs,
    int StepCount,
    string ValidationStatus,
    string MotionStatus,
    bool HardShakeVeto,
    bool MotionEvidenceLifecycleClosed = false);

public sealed record StepSupportResolution(
    Guid RecordId,
    string Status,
    string Reason);

public sealed record StepSupportAllocation(
    IReadOnlyList<Guid> CandidatesToAccept,
    IReadOnlyList<StepSupportResolution> FinalResolutions,
    int ConsumedSupportBudget,
    int RemainingSupportBudget);

public static class StepSupportBudgetRules
{
    public static StepSupportAllocation Allocate(
        int supportBudget,
        IEnumerable<StepSupportCandidate> candidates)
    {
        var ordered = candidates
            .OrderBy(x => x.SensorElapsedRealtimeNs)
            .ThenBy(x => x.ClientEventId)
            .ToArray();
        var normalizedBudget = Math.Max(0, supportBudget);
        var alreadyConsumed = ordered
            .Where(x => x.ValidationStatus == "accepted")
            .Sum(x => Math.Max(0, x.StepCount));
        var remaining = Math.Max(0, normalizedBudget - alreadyConsumed);
        var toAccept = new List<Guid>();
        var resolutions = new List<StepSupportResolution>();

        foreach (var candidate in ordered.Where(x => x.ValidationStatus == "pending"))
        {
            var motionStatus = candidate.MotionStatus.Trim().ToLowerInvariant();
            if (motionStatus == "rejected")
            {
                resolutions.Add(new(
                    candidate.RecordId,
                    "rejected",
                    "motion_rejected_after_reconciliation"));
                continue;
            }
            if (motionStatus == "suspicious")
            {
                resolutions.Add(new(
                    candidate.RecordId,
                    "suspicious",
                    "motion_suspicious_after_reconciliation"));
                continue;
            }
            if (motionStatus != "accepted")
            {
                resolutions.Add(new(
                    candidate.RecordId,
                    candidate.MotionEvidenceLifecycleClosed ? "suspicious" : "pending",
                    candidate.MotionEvidenceLifecycleClosed
                        ? "motion_unavailable_after_settlement"
                        : "pending_motion_evidence"));
                continue;
            }
            if (candidate.HardShakeVeto)
            {
                resolutions.Add(new(
                    candidate.RecordId,
                    "suspicious",
                    "hard_shake_batch_veto"));
                continue;
            }

            var stepCount = Math.Max(0, candidate.StepCount);
            if (stepCount > 0 && stepCount <= remaining)
            {
                toAccept.Add(candidate.RecordId);
                remaining -= stepCount;
                continue;
            }

            resolutions.Add(new(
                candidate.RecordId,
                "suspicious",
                "counter_support_budget_exhausted"));
        }

        return new(
            toAccept,
            resolutions,
            normalizedBudget - remaining,
            remaining);
    }

    public static int CalculateIncrementalCounterExcess(
        int detectorSteps,
        int counterDelta,
        int existingCounterExcess)
    {
        var totalCounterExcess = Math.Max(
            0,
            Math.Max(0, counterDelta) - Math.Max(0, detectorSteps));
        return Math.Max(0, totalCounterExcess - Math.Max(0, existingCounterExcess));
    }
}
