namespace DAL.Models;

public partial class PvpPlayerProfile
{
    public Guid UserId { get; set; }
    public int Mmr { get; set; } = 1000;
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
    public string? SpiritAffinityCode { get; set; }
    public byte PetStageNo { get; set; } = 1;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
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
    public DateTime ReceivedAt { get; set; }
    public PvpStepSession StepSession { get; set; } = null!;
    public ICollection<ValidatedStepRecord> Records { get; set; } = new List<ValidatedStepRecord>();
    public ICollection<StepMotionEvidenceWindow> MotionWindows { get; set; } = new List<StepMotionEvidenceWindow>();
}

public partial class StepMotionEvidenceWindow
{
    public Guid StepMotionEvidenceWindowId { get; set; }
    public Guid BatchId { get; set; }
    public short WindowIndex { get; set; }
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
