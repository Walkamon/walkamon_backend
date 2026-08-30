namespace DAL.Models;

public partial class PvpPlayerProfile
{
    public Guid UserId { get; set; }
    public int Mmr { get; set; } = 1000;
    public short ConsecutiveValidRankedLosses { get; set; }
    public int CompletedRankedMatchesSinceRelief { get; set; }
    public DateTime? LastReliefCompletedAt { get; set; }
    public string? LastBotDifficultyCode { get; set; }
    public byte ConsecutiveHardBotCount { get; set; }
    public byte[] RowVersion { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
    public User User { get; set; } = null!;
}

public partial class PvpPlayerActivity
{
    public Guid UserId { get; set; }
    public string ActivityType { get; set; } = null!;
    public Guid ActivityId { get; set; }
    public DateTime? DueAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User User { get; set; } = null!;
}

public partial class PvpBotProfile
{
    public Guid BotProfileId { get; set; }
    public string DisplayName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public int Mmr { get; set; }
    public decimal StepsPerSecond { get; set; }
    public string DifficultyCode { get; set; } = "fair";
    public int MinPaceMilli { get; set; } = 1000;
    public int MaxPaceMilli { get; set; } = 2500;
    public short TargetUserWinMinBps { get; set; } = 4500;
    public short TargetUserWinMaxBps { get; set; } = 5500;
    public short ItemPowerBudgetBps { get; set; } = 1000;
    public int ProfileVersion { get; set; } = 1;
    public byte[] RowVersion { get; set; } = null!;
    public string? SpiritAffinityCode { get; set; }
    public byte PetStageNo { get; set; } = 1;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public partial class PvpMatchmakingPolicy
{
    public int PolicyVersion { get; set; }
    public bool IsActive { get; set; }
    public byte MatchDurationSeconds { get; set; } = 30;
    public byte BotFallbackSeconds { get; set; } = 10;
    public short Stage1MmrGap { get; set; } = 75;
    public short Stage1PowerGapBps { get; set; } = 800;
    public short Stage1PaceRatioBps { get; set; } = 11000;
    public short Stage2MmrGap { get; set; } = 100;
    public short Stage2PowerGapBps { get; set; } = 1200;
    public short Stage2PaceRatioBps { get; set; } = 11500;
    public short Stage3MmrGap { get; set; } = 150;
    public short Stage3PowerGapBps { get; set; } = 1500;
    public short Stage3PaceRatioBps { get; set; } = 12000;
    public short HardMmrGap { get; set; } = 250;
    public short HardPowerGapBps { get; set; } = 2000;
    public short HardPaceRatioBps { get; set; } = 12500;
    public short Streak01EasyWeightBps { get; set; } = 2000;
    public short Streak01FairWeightBps { get; set; } = 5000;
    public short Streak01HardWeightBps { get; set; } = 3000;
    public short Streak23EasyWeightBps { get; set; } = 4500;
    public short Streak23FairWeightBps { get; set; } = 4500;
    public short Streak23HardWeightBps { get; set; } = 1000;
    public short Streak4EasyWeightBps { get; set; } = 7000;
    public short Streak4FairWeightBps { get; set; } = 3000;
    public short Streak4HardWeightBps { get; set; }
    public byte ReliefLossThreshold { get; set; } = 5;
    public short ReliefTargetUserWinBps { get; set; } = 8200;
    public short EasyTargetUserWinBps { get; set; } = 8200;
    public short FairTargetUserWinBps { get; set; } = 5000;
    public short HardTargetUserWinBps { get; set; } = 3000;
    public byte BotHistoryWindow { get; set; } = 10;
    public byte MaxBotMatchesInWindow { get; set; } = 6;
    public bool AllowConsecutiveHard { get; set; }
    public short EasyWinMmrDelta { get; set; }
    public short EasyDrawMmrDelta { get; set; }
    public short EasyLossMmrDelta { get; set; } = -1;
    public short FairWinMmrDelta { get; set; } = 2;
    public short FairDrawMmrDelta { get; set; }
    public short FairLossMmrDelta { get; set; } = -2;
    public short HardWinMmrDelta { get; set; } = 6;
    public short HardDrawMmrDelta { get; set; }
    public short HardLossMmrDelta { get; set; } = -2;
    public short ReliefWinMmrDelta { get; set; }
    public short ReliefDrawMmrDelta { get; set; }
    public short ReliefLossMmrDelta { get; set; }
    public byte BotRatingWindow { get; set; } = 20;
    public short MaxPositiveBotMmrInWindow { get; set; } = 8;
    public short EasyRewardMultiplierBps { get; set; } = 2500;
    public short FairRewardMultiplierBps { get; set; } = 5000;
    public short HardRewardMultiplierBps { get; set; } = 10000;
    public short ReliefRewardMultiplierBps { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
}

public partial class PvpSprintInvite
{
    public Guid InviteId { get; set; }
    public Guid InviterUserId { get; set; }
    public Guid InviteeUserId { get; set; }
    public Guid UserLowId { get; set; }
    public Guid UserHighId { get; set; }
    public string StatusCode { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public Guid? MatchId { get; set; }
    public DateTime CreatedAt { get; set; }
    public User InviterUser { get; set; } = null!;
    public User InviteeUser { get; set; } = null!;
    public PvpMatch? Match { get; set; }
}

public partial class PvpStepSession
{
    public Guid StepSessionId { get; set; }
    public Guid? MatchId { get; set; }
    public Guid UserId { get; set; }
    public string PurposeCode { get; set; } = null!;
    public string PlatformCode { get; set; } = null!;
    public string SensorModeCode { get; set; } = null!;
    public int ContractVersion { get; set; } = 2;
    public string? CaptureMetadataJson { get; set; }
    public string Nonce { get; set; } = null!;
    public string StatusCode { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSubmittedAt { get; set; }
    public int LastSequence { get; set; }
    public long? LastSensorTotal { get; set; }
    public DateTime? LastRecordedAt { get; set; }
    public string? ClosedReason { get; set; }
    public byte[] RowVersion { get; set; } = null!;
    public PvpMatch? Match { get; set; }
    public User User { get; set; } = null!;
    public ICollection<StepSensorBatch> Batches { get; set; } = new List<StepSensorBatch>();
}

public partial class StepSensorBatch
{
    public Guid StepSensorBatchId { get; set; }
    public Guid StepSessionId { get; set; }
    public int Sequence { get; set; }
    public string PayloadHash { get; set; } = null!;
    public string AttestationStatus { get; set; } = null!;
    public string? PackageName { get; set; }
    public DateTime? VerdictTimestamp { get; set; }
    public string? VerdictJson { get; set; }
    public int EvidenceVersion { get; set; }
    public int MotionScore { get; set; }
    public string MotionStatus { get; set; } = "unavailable";
    public string MotionReasonsJson { get; set; } = "[]";
    public bool DegradedEvidence { get; set; }
    public int AcceptedSteps { get; set; }
    public int RejectedSteps { get; set; }
    public int SuspiciousSteps { get; set; }
    public string ReconciliationStatus { get; set; } = "unavailable";
    public string? ReconciliationReason { get; set; }
    public DateTime ReceivedAt { get; set; }
    public PvpStepSession StepSession { get; set; } = null!;
    public ICollection<ValidatedStepRecord> Records { get; set; } = new List<ValidatedStepRecord>();
    public ICollection<StepMotionEvidenceWindow> MotionWindows { get; set; } = new List<StepMotionEvidenceWindow>();
    public ICollection<StepCounterEvidenceSample> CounterSamples { get; set; } = new List<StepCounterEvidenceSample>();
}

public partial class StepCounterEvidenceSample
{
    public Guid CounterSampleId { get; set; }
    public Guid BatchId { get; set; }
    public short SampleIndex { get; set; }
    public Guid ClientSampleId { get; set; }
    public Guid BootSessionId { get; set; }
    public long SensorElapsedRealtimeNs { get; set; }
    public DateTime ObservedAt { get; set; }
    public long CounterTotal { get; set; }
    public StepSensorBatch Batch { get; set; } = null!;
}

public partial class StepMotionEvidenceWindow
{
    public Guid StepMotionEvidenceWindowId { get; set; }
    public Guid BatchId { get; set; }
    public short WindowIndex { get; set; }
    public Guid? BootSessionId { get; set; }
    public long? WindowStartElapsedRealtimeNs { get; set; }
    public long? WindowEndElapsedRealtimeNs { get; set; }
    public DateTime WindowStartedAt { get; set; }
    public DateTime WindowEndedAt { get; set; }
    public short SampleCount { get; set; }
    public string AccelerometerSource { get; set; } = null!;
    public bool GyroscopeAvailable { get; set; }
    public bool ActivityAvailable { get; set; }
    public int AccelerationRmsMilli { get; set; }
    public int AccelerationPeakMilli { get; set; }
    public int JerkRmsMilli { get; set; }
    public int? GyroscopeRmsMilli { get; set; }
    public int? GyroscopePeakMilli { get; set; }
    public int? OrientationDeltaMilliDegrees { get; set; }
    public int DominantFrequencyMilliHz { get; set; }
    public int PeriodicityBps { get; set; }
    public short GaitCycleCount { get; set; }
    public string ActivityCode { get; set; } = "unknown";
    public byte ActivityConfidence { get; set; }
    public byte MotionScore { get; set; }
    public string Classification { get; set; } = null!;
    public string ReasonCodes { get; set; } = "[]";
    public StepSensorBatch Batch { get; set; } = null!;
}

public partial class ValidatedStepRecord
{
    public Guid ValidatedStepRecordId { get; set; }
    public Guid UserId { get; set; }
    public Guid? StepSessionId { get; set; }
    public Guid? BatchId { get; set; }
    public int? EventIndex { get; set; }
    public Guid? ClientEventId { get; set; }
    public Guid? BootSessionId { get; set; }
    public long? SensorElapsedRealtimeNs { get; set; }
    public string PlatformCode { get; set; } = null!;
    public string SourceCode { get; set; } = null!;
    public string SensorModeCode { get; set; } = null!;
    public DateTime IntervalStartedAt { get; set; }
    public DateTime RecordedAt { get; set; }
    public long? SensorStartTotal { get; set; }
    public long? SensorEndTotal { get; set; }
    public int StepCount { get; set; }
    public int EligibleStepCount { get; set; }
    public int SequenceNumber { get; set; }
    public string PayloadHash { get; set; } = null!;
    public string ValidationStatus { get; set; } = null!;
    public string? RejectionReason { get; set; }
    public int MotionScore { get; set; }
    public string MotionStatus { get; set; } = "unavailable";
    public DateTime ReceivedAt { get; set; }
    public User User { get; set; } = null!;
    public PvpStepSession? StepSession { get; set; }
    public StepSensorBatch? Batch { get; set; }
}

public partial class PvpRewardRule
{
    public Guid PvpRewardRuleId { get; set; }
    public string MatchTypeCode { get; set; } = null!;
    public string ResultCode { get; set; } = null!;
    public Guid RewardPackageId { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
    public RewardPackage RewardPackage { get; set; } = null!;
}

public partial class PvpMatchRewardEntitlement
{
    public Guid MatchRewardEntitlementId { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public string ResultCode { get; set; } = null!;
    public int WalletAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public PvpMatch Match { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<PvpMatchRewardItem> Items { get; set; } = new List<PvpMatchRewardItem>();
}

public partial class PvpMatchRewardItem
{
    public Guid MatchRewardEntitlementId { get; set; }
    public Guid ItemId { get; set; }
    public int Quantity { get; set; }
    public PvpMatchRewardEntitlement Entitlement { get; set; } = null!;
    public Item Item { get; set; } = null!;
}

public partial class PvpMatchRewardSnapshot
{
    public Guid MatchRewardSnapshotId { get; set; }
    public Guid MatchId { get; set; }
    public string ResultCode { get; set; } = null!;
    public int WalletAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public PvpMatch Match { get; set; } = null!;
    public ICollection<PvpMatchRewardSnapshotItem> Items { get; set; } = new List<PvpMatchRewardSnapshotItem>();
}

public partial class PvpMatchRewardSnapshotItem
{
    public Guid MatchRewardSnapshotId { get; set; }
    public Guid ItemId { get; set; }
    public int Quantity { get; set; }
    public PvpMatchRewardSnapshot Snapshot { get; set; } = null!;
    public Item Item { get; set; } = null!;
}

public partial class PvpMatchEvent
{
    public Guid PvpMatchEventId { get; set; }
    public Guid MatchId { get; set; }
    public long Sequence { get; set; }
    public string EventType { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public PvpMatch Match { get; set; } = null!;
}

public partial class OutboxEvent
{
    public Guid EventId { get; set; }
    public string AggregateType { get; set; } = null!;
    public Guid AggregateId { get; set; }
    public string Destination { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public int Attempts { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
