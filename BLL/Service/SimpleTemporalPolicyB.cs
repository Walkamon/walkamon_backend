namespace BLL.Service;

public static class SimpleTemporalPolicyBConstants
{
    public const string ValidationMode = "simple_counter_temporal";
    public const string Revision = "temporal_v2_policy_b";
    public const string Provenance = "WALKAMON_DEVSET_EXPERIMENTAL";

    // Selected from the Walkamon development benchmark. These are
    // application-specific experimental anti-fraud parameters, not Android
    // platform thresholds, and are not independently cross-device validated.
    public const long SustainedFraudDurationMs = 17_000;
    public const int RepeatedFraudRegionCount = 2;
}

public static class SimpleTemporalPolicyBReasonCodes
{
    public const string Allowed = "temporal_policy_b_allow";
    public const string SustainedFraud = "temporal_fraud_sustained";
    public const string RepeatedFraud = "temporal_fraud_repeated";
    public const string SecurityFailed = "security_validation_failed";
    public const string CounterIntervalInvalid = "counter_interval_invalid";
}

public sealed record SimpleTemporalPolicyBInput(
    long CounterDelta,
    int FraudRegionCount,
    long MaxFraudRegionDurationMs,
    bool SecurityValid = true,
    bool CounterIntervalValid = true);

public sealed record SimpleTemporalPolicyBResult(
    string ValidationMode,
    string PolicyRevision,
    string PolicyProvenance,
    string Decision,
    long CounterDelta,
    long EligibleStepCount,
    IReadOnlyList<string> ReasonCodes);

public static class SimpleTemporalPolicyB
{
    public static SimpleTemporalPolicyBResult Evaluate(
        SimpleTemporalPolicyBInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var reasons = new List<string>();
        if (!input.SecurityValid)
            reasons.Add(SimpleTemporalPolicyBReasonCodes.SecurityFailed);
        if (!input.CounterIntervalValid || input.CounterDelta < 0)
            reasons.Add(SimpleTemporalPolicyBReasonCodes.CounterIntervalInvalid);

        if (reasons.Count == 0)
        {
            if (input.MaxFraudRegionDurationMs >=
                SimpleTemporalPolicyBConstants.SustainedFraudDurationMs)
                reasons.Add(SimpleTemporalPolicyBReasonCodes.SustainedFraud);
            if (input.FraudRegionCount >=
                SimpleTemporalPolicyBConstants.RepeatedFraudRegionCount)
                reasons.Add(SimpleTemporalPolicyBReasonCodes.RepeatedFraud);
        }

        var blocked = reasons.Any(x =>
            x != SimpleTemporalPolicyBReasonCodes.Allowed);
        if (!blocked)
            reasons.Add(SimpleTemporalPolicyBReasonCodes.Allowed);

        var decision = blocked
            ? SimpleTemporalPolicyDecisions.Block
            : SimpleTemporalPolicyDecisions.Allow;
        var counterDelta = Math.Max(0, input.CounterDelta);
        return new(
            SimpleTemporalPolicyBConstants.ValidationMode,
            SimpleTemporalPolicyBConstants.Revision,
            SimpleTemporalPolicyBConstants.Provenance,
            decision,
            counterDelta,
            decision == SimpleTemporalPolicyDecisions.Allow ? counterDelta : 0,
            reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
}

public sealed record SimpleAuthoritativeTransition(
    bool IsNewAuthoritativeTransition,
    long NewlyAuthoritativeSteps);

public static class SimpleAuthoritativeTransitionRules
{
    public static SimpleAuthoritativeTransition Evaluate(
        SimpleTemporalPolicyBResult policy,
        bool authoritativeEnabled,
        bool alreadyAuthoritative)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var apply = authoritativeEnabled &&
                    !alreadyAuthoritative &&
                    policy.Decision == SimpleTemporalPolicyDecisions.Allow &&
                    policy.EligibleStepCount > 0;
        return new(apply, apply ? policy.EligibleStepCount : 0);
    }
}

public static class SimpleTemporalSettlementRules
{
    public static bool IsFinal(
        DateTime counterEndpointReceivedAt,
        DateTime now,
        int counterSettlementSeconds,
        int pendingDetectorCount)
    {
        var endpoint = AsUtc(counterEndpointReceivedAt);
        var current = AsUtc(now);
        return pendingDetectorCount <= 0 &&
               endpoint.AddSeconds(Math.Max(1, counterSettlementSeconds)) <= current;
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
