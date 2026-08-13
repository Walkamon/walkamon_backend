namespace BLL.Options;

public sealed class MotionValidationOptions
{
    public const string SectionName = "MotionValidation";

    public bool Enabled { get; set; } = true;
    public bool Enforce { get; set; } = true;
    public int ContractVersion { get; set; } = 2;
    public int WindowMilliseconds { get; set; } = 1000;
    public int TargetSampleHz { get; set; } = 25;
    public int MinSamplesPerWindow { get; set; } = 15;
    public int MaxSamplesPerWindow { get; set; } = 40;
    public int MinCoverageBps { get; set; } = 8000;
    public int AcceptedScore { get; set; } = 70;
    public int RejectedScore { get; set; } = 40;
    public int ActivityConfidenceThreshold { get; set; } = 70;
    public int MinGaitAgreementBps { get; set; } = 6000;
    public int PartialGaitAgreementBps { get; set; } = 8000;
    public int ShakeAccelerationPeakMilli { get; set; } = 20000;
    public int ShakeJerkRmsMilli { get; set; } = 30000;
    public int ShakeGyroscopeRmsMilli { get; set; } = 3000;
    public int ShakeGyroscopePeakMilli { get; set; } = 7000;
    public int ShakeAngularTravelMilliDegrees { get; set; } = 120000;
    public int MachinePeriodicityBps { get; set; } = 9200;
    public int MachineCadenceVariationBps { get; set; } = 300;
    public int MaxCadenceMilliHz { get; set; } = 4000;
    public string ThresholdProvenance { get; set; } = "EXPERIMENTAL";
}
