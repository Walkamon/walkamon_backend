namespace BLL.Service;

public sealed record StepReconciliationDecision(
    int PendingDetectorSteps,
    int SupportBudget,
    int CounterExcessSteps,
    string Status,
    string? Reason);

public static class StepReconciliationRules
{
    public static StepReconciliationDecision Evaluate(
        int detectorSteps,
        int counterDelta,
        bool settlementClosed)
    {
        detectorSteps = Math.Max(0, detectorSteps);
        counterDelta = Math.Max(0, counterDelta);
        if (detectorSteps == counterDelta)
            return new(0, detectorSteps, 0, "accepted", null);
        if (!settlementClosed)
            return new(
                detectorSteps,
                0,
                0,
                "pending_reconciliation",
                "counter_reconciliation_pending");

        var supportBudget = Math.Min(detectorSteps, counterDelta);
        var counterExcess = Math.Max(0, counterDelta - detectorSteps);
        return new(
            0,
            supportBudget,
            counterExcess,
            "suspicious",
            detectorSteps > counterDelta
                ? "counter_mismatch_settled"
                : "counter_excess_settled");
    }
}
