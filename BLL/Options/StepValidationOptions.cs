namespace BLL.Options;

public sealed class StepValidationOptions
{
    public const string SectionName = "StepValidation";
    public bool StrictAttestation { get; set; } = true;
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
}
