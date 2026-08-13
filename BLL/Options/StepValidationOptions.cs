namespace BLL.Options;

public sealed class StepValidationOptions
{
    public const string SectionName = "StepValidation";
    public bool StrictAttestation { get; set; } = true;
    public bool RequirePerBatchAttestation { get; set; } = true;
    public string? AndroidPackageName { get; set; }
    public string? GoogleCredentialPath { get; set; }
    public string AppRecognitionMode { get; set; } = "play";
    public string? AllowedCertificateSha256Hex { get; set; }
    public int MinimumVersionCode { get; set; } = 1;
    public bool RequireDeviceIntegrity { get; set; } = true;
    public bool RequireLicensingVerdict { get; set; }
    public int AttestationMaxAgeSeconds { get; set; } = 120;
    public int DailyBatchMaxAgeSeconds { get; set; } = 120;
    public int FutureToleranceSeconds { get; set; } = 2;
    public int MaxBatchEvents { get; set; } = 100;
    public int MaxBatchCounterSamples { get; set; } = 100;
    public int MaxBatchMotionWindows { get; set; } = 130;
    // CURRENT_WALKAMON_SECURITY_POLICY / EXPERIMENTAL. This is not an Android
    // sensor rule and is not guaranteed by Play Integrity requestHash.
    public int MaxEvidenceAgeSeconds { get; set; } = 120;
    // EXPERIMENTAL: authoritative rollout must not use this value until benchmarked.
    public int CounterSettlementSeconds { get; set; } = 15;
    public bool V3AuthoritativeEnabled { get; set; }
    // Counter-aggregate validation can continue evaluating in shadow while its
    // independent authoritative kill switch remains disabled.
    public bool SimpleStepValidationEnabled { get; set; }
    public string SimpleStepValidationRevision { get; set; } = "temporal_v2_policy_b";
    public bool SimpleStepValidationAuthoritativeEnabled { get; set; }
    // Backward-compatible configuration surface for the earlier shadow tools.
    // Runtime authority is controlled only by
    // SimpleStepValidationAuthoritativeEnabled.
    public bool SimpleStepValidationShadowOnly { get; set; } = true;
}

public static class StepValidationConfigurationValidator
{
    public static void Validate(StepValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.V3AuthoritativeEnabled &&
            options.SimpleStepValidationAuthoritativeEnabled)
            throw new InvalidOperationException(
                "V3 and Simple step validation cannot both be authoritative.");
        if (options.SimpleStepValidationAuthoritativeEnabled &&
            !options.SimpleStepValidationEnabled)
            throw new InvalidOperationException(
                "Simple authoritative validation requires SimpleStepValidationEnabled=true.");
        if (!string.Equals(
                options.SimpleStepValidationRevision,
                BLL.Service.SimpleTemporalPolicyBConstants.Revision,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Unknown Simple step validation revision '{options.SimpleStepValidationRevision}'. " +
                $"Expected '{BLL.Service.SimpleTemporalPolicyBConstants.Revision}'.");
    }
}
