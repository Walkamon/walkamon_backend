namespace BLL.Service;

public static class StepMotionEvidenceRules
{
    public const string MissingReason = "motion_evidence_missing";
    public const string PendingReason = "pending_motion_evidence";

    public static string NormalizeStatus(
        string evaluatedStatus,
        IEnumerable<string> reasons) =>
        reasons.Contains(MissingReason, StringComparer.Ordinal)
            ? "unavailable"
            : evaluatedStatus.Trim().ToLowerInvariant();

    public static bool IsLifecycleClosed(
        DateTime recordedAt,
        DateTime now,
        int maxEvidenceAgeSeconds) =>
        recordedAt.AddSeconds(Math.Max(1, maxEvidenceAgeSeconds)) <= now;
}
