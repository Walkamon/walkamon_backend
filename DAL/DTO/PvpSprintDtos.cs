namespace DAL.DTO;

public sealed class CreatePvpSprintInviteRequest { public Guid TargetUserId { get; set; } }
public sealed class RespondPvpSprintInviteRequest { public bool Accept { get; set; } }
public sealed class JoinPvpMatchmakingRequest { public string MatchTypeCode { get; set; } = "ranked"; }
public sealed class CreatePvpStepSessionRequest
{
    public int ContractVersion { get; set; } = 2;
    public string PlatformCode { get; set; } = null!;
    public string? SensorModeCode { get; set; }
    public string? CaptureMode { get; set; }
    public System.Text.Json.JsonElement? CaptureMetadata { get; set; }
}
public sealed class SubmitPvpStepBatchRequest
{
    public int ContractVersion { get; set; } = 1;
    public int Sequence { get; set; }
    public string Nonce { get; set; } = null!;
    public string AttestationToken { get; set; } = null!;
    public string PayloadHash { get; set; } = null!;
    public List<PvpStepEventRequest> Events { get; set; } = [];
    public List<StepDetectorEventRequest> DetectorEvents { get; set; } = [];
    public List<StepCounterSampleRequest> CounterSamples { get; set; } = [];
    public List<StepMotionWindowRequest> MotionWindows { get; set; } = [];
}
public sealed class StepDetectorEventRequest
{
    public Guid ClientEventId { get; set; }
    public Guid BootSessionId { get; set; }
    public long SensorElapsedRealtimeNs { get; set; }
    public DateTime RecordedAt { get; set; }
}
public sealed class StepCounterSampleRequest
{
    public Guid ClientSampleId { get; set; }
    public Guid BootSessionId { get; set; }
    public long SensorElapsedRealtimeNs { get; set; }
    public DateTime ObservedAt { get; set; }
    public long CounterTotal { get; set; }
}
public sealed class PvpStepEventRequest
{
    public DateTime IntervalStartedAt { get; set; }
    public DateTime RecordedAt { get; set; }
    public int StepCount { get; set; }
    public long? SensorStartTotal { get; set; }
    public long? SensorEndTotal { get; set; }
}
public sealed class StepMotionWindowRequest
{
    public Guid BootSessionId { get; set; }
    public long WindowStartElapsedRealtimeNs { get; set; }
    public long WindowEndElapsedRealtimeNs { get; set; }
    public DateTime WindowStartedAt { get; set; }
    public DateTime WindowEndedAt { get; set; }
    public int SampleCount { get; set; }
    public string AccelerometerSource { get; set; } = null!;
    public bool GyroscopeAvailable { get; set; }
    public bool ActivityAvailable { get; set; }
    public int AccelerationRmsMilli { get; set; }
    public int AccelerationPeakMilli { get; set; }
    public int JerkRmsMilli { get; set; }
    public int? GyroscopeRmsMilli { get; set; }
    public int? GyroscopePeakMilli { get; set; }
    public int? OrientationDeltaMilliDegrees { get; set; }
    public int? AngularTravelMilliDegrees
    {
        get => OrientationDeltaMilliDegrees;
        set => OrientationDeltaMilliDegrees = value;
    }
    public int DominantFrequencyMilliHz { get; set; }
    public int PeriodicityBps { get; set; }
    public int GaitCycleCount { get; set; }
    public string ActivityCode { get; set; } = "unknown";
    public int ActivityConfidence { get; set; }
}
public sealed class PvpRewardItemRequest { public Guid ItemId { get; set; } public int Quantity { get; set; } }
public class PvpRewardRuleRequest
{
    public string MatchTypeCode { get; set; } = null!;
    public string ResultCode { get; set; } = null!;
    public int WalletAmount { get; set; }
    public List<PvpRewardItemRequest> RewardItems { get; set; } = [];
}
public sealed class UpdatePvpRewardRulesRequest { public List<PvpRewardRuleRequest> Rules { get; set; } = []; }

public sealed class PvpUserSummaryResponse { public Guid UserId { get; set; } public string? Username { get; set; } public string? AvatarUrl { get; set; } }
public sealed class PvpInviteResponse
{
    public Guid InviteId { get; set; }
    public PvpUserSummaryResponse User { get; set; } = null!;
    public bool OtherUserIsOnline { get; set; }
    public string OtherUserPvpAvailabilityCode { get; set; } = "offline";
    public string StatusCode { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? MatchId { get; set; }
}
public sealed class PvpParticipantResponse
{
    public Guid MatchPlayerId { get; set; }
    public string ParticipantTypeCode { get; set; } = null!;
    public Guid? UserId { get; set; }
    public Guid? BotProfileId { get; set; }
    public string DisplayName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public Guid? PetId { get; set; }
    public string? PetName { get; set; }
    public int PetLevel { get; set; }
    public int PetStageNo { get; set; }
    public string PetVisualCode { get; set; } = "sprout_stage0";
    public int Score { get; set; }
    public int ValidatedSteps { get; set; }
    public int DailyEligibleStepsSnapshot { get; set; }
    public int BasePaceMilliStepsPerSecond { get; set; }
    public long DistanceUnits { get; set; }
    public int SpeedMultiplierBps { get; set; } = 10000;
    public string? SpiritAffinityCode { get; set; }
    public int PassiveSpeedBps { get; set; }
    public bool IsReady { get; set; }
    public string? ResultCode { get; set; }
}
public class PvpMatchResponse
{
    public Guid MatchId { get; set; }
    public string MatchTypeCode { get; set; } = null!;
    public string SourceCode { get; set; } = null!;
    public string StatusCode { get; set; } = null!;
    public string? FinishReasonCode { get; set; }
    public string? CancelReasonCode { get; set; }
    public Guid? ForfeitedByUserId { get; set; }
    public Guid? WinnerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CountdownStartsAt { get; set; }
    public DateTime? CountdownEndsAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? SettlementEndsAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime ServerTime { get; set; }
    public int RuleVersion { get; set; }
    public string ScoringModeCode { get; set; } = "daily_power_v1";
    public int DailyStepPowerCap { get; set; }
    public long LastEventSequence { get; set; }
    public List<PvpMatchEffectResponse> ActiveEffects { get; set; } = [];
    public List<PvpMatchLoadoutSlotResponse> Loadout { get; set; } = [];
    public List<PvpParticipantResponse> Participants { get; set; } = [];
}
public sealed class PvpMatchReadyResponse
{
    public Guid MatchId { get; set; }
    public string StatusCode { get; set; } = null!;
    public bool AllReady { get; set; }
    public DateTime? CountdownStartsAt { get; set; }
    public DateTime? CountdownEndsAt { get; set; }
    public long LastEventSequence { get; set; }
    public DateTime ServerTime { get; set; }
}
public sealed class PvpMatchmakingStatusResponse
{
    public string ActivityType { get; set; } = "idle";
    public string StatusCode { get; set; } = "idle";
    public Guid? MatchId { get; set; }
    public DateTime? CountdownStartsAt { get; set; }
    public DateTime? CountdownEndsAt { get; set; }
    public DateTime? QueuedAt { get; set; }
    public DateTime? BotFallbackAt { get; set; }
    public DateTime ServerTime { get; set; }
}
public sealed class PvpResultResponse : PvpMatchResponse
{
    public int MmrBefore { get; set; }
    public int MmrDelta { get; set; }
    public int MmrAfter { get; set; }
    public PvpRankTierResponse RankBefore { get; set; } = null!;
    public PvpRankTierResponse RankAfter { get; set; } = null!;
    public bool TierChanged { get; set; }
    public bool CanClaimReward { get; set; }
    public DateTime? ClaimedAt { get; set; }
}
public sealed class PvpPagedResponse<T> { public int Page { get; set; } public int PageSize { get; set; } public int Total { get; set; } public List<T> Items { get; set; } = []; }
public sealed class PvpStepSessionResponse
{
    public Guid StepSessionId { get; set; }
    public string Nonce { get; set; } = null!;
    public string PurposeCode { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public int NextSequence { get; set; }
    public DateTime ServerTime { get; set; }
    public string? DailyStepDate { get; set; }
    public int? DailyAcceptedTotal { get; set; }
    public int ContractVersion { get; set; } = 2;
    public string CaptureMode { get; set; } = "detector";
    public StepMotionPolicyResponse MotionPolicy { get; set; } = new();
}
public sealed class StepMotionPolicyResponse
{
    public int ContractVersion { get; set; } = 2;
    public bool Required { get; set; } = true;
    public int WindowMilliseconds { get; set; } = 1000;
    public int TargetSampleHz { get; set; } = 25;
    public int MinSamplesPerWindow { get; set; } = 15;
    public int MaxSamplesPerWindow { get; set; } = 40;
}
public sealed class PvpStepBatchResponse
{
    public Guid BatchId { get; set; }
    public string AttestationStatus { get; set; } = "unavailable";
    public int AcceptedSteps { get; set; }
    public int PendingSteps { get; set; }
    public int RejectedSteps { get; set; }
    public int SuspiciousSteps { get; set; }
    public int NextSequence { get; set; }
    public string? DailyStepDate { get; set; }
    public int? DailyAcceptedTotal { get; set; }
    public int CurrentScore { get; set; }
    public int ValidatedSteps { get; set; }
    public long DistanceUnits { get; set; }
    public int SpeedMultiplierBps { get; set; }
    public string MotionStatus { get; set; } = "unavailable";
    public int MotionScore { get; set; }
    public bool DegradedEvidence { get; set; }
    public List<string> MotionReasons { get; set; } = [];
    public string ReconciliationStatus { get; set; } = "unavailable";
    public string? ReconciliationReason { get; set; }
    public List<StepDetectorResolutionResponse> DetectorResolutions { get; set; } = [];
}
public sealed class StepDetectorResolutionResponse
{
    public Guid ClientEventId { get; set; }
    public string Status { get; set; } = "pending";
    public int AcceptedStepCount { get; set; }
    public string? Reason { get; set; }
}
public sealed class PvpRewardClaimResponse { public int WalletBalance { get; set; } public int WalletReward { get; set; } public List<PvpRewardItemRequest> RewardItems { get; set; } = []; }
public sealed class PvpRewardRuleResponse : PvpRewardRuleRequest { public bool IsActive { get; set; } }

public sealed class PvpLoadoutSlotRequest { public byte SlotNo { get; set; } public Guid ItemId { get; set; } }
public sealed class UpdatePvpLoadoutRequest { public List<PvpLoadoutSlotRequest> Slots { get; set; } = []; }
public sealed class UsePvpItemRequest { public byte SlotNo { get; set; } public Guid ClientActionId { get; set; } }

public sealed class PvpMatchLoadoutSlotResponse
{
    public Guid? MatchLoadoutSlotId { get; set; }
    public byte SlotNo { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string EffectCode { get; set; } = null!;
    public string AssetKey { get; set; } = null!;
    public int Quantity { get; set; }
    public DateTime? UsedAt { get; set; }
}

public sealed class PvpLoadoutResponse { public List<PvpMatchLoadoutSlotResponse> Slots { get; set; } = []; }

public sealed class PvpMatchEffectResponse
{
    public Guid EffectId { get; set; }
    public Guid TargetMatchPlayerId { get; set; }
    public string EffectCode { get; set; } = null!;
    public string EffectKindCode { get; set; } = null!;
    public int MagnitudeBps { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
}

public sealed class UsePvpItemResponse
{
    public Guid ActionId { get; set; }
    public Guid ClientActionId { get; set; }
    public string ResultCode { get; set; } = null!;
    public string EffectCode { get; set; } = null!;
    public int RemainingQuantity { get; set; }
    public DateTime ServerTime { get; set; }
    public PvpMatchEffectResponse? Effect { get; set; }
}

public sealed class PvpRankTierResponse
{
    public string TierCode { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public int MinMmr { get; set; }
    public string AssetKey { get; set; } = null!;
    public string ColorHex { get; set; } = null!;
}

public class PvpProfileResponse
{
    public Guid UserId { get; set; }
    public int Mmr { get; set; }
    public int Position { get; set; }
    public PvpRankTierResponse Tier { get; set; } = null!;
}

public sealed class PvpRankingEntryResponse : PvpProfileResponse
{
    public string Username { get; set; } = null!;
    public string? AvatarUrl { get; set; }
}

public sealed class PvpItemEffectAdminRequest
{
    public string EffectCode { get; set; } = null!;
    public int MagnitudeBps { get; set; }
    public int DurationMs { get; set; }
    public int CooldownMs { get; set; }
    public string AssetKey { get; set; } = null!;
    public bool IsActive { get; set; }
}

public sealed class UpdatePvpItemEffectsRequest { public List<PvpItemEffectAdminRequest> Effects { get; set; } = []; }
public sealed class PvpSpiritRuleAdminRequest { public string AffinityCode { get; set; } = null!; public int StartMinute { get; set; } public int EndMinute { get; set; } public int BonusBps { get; set; } public bool IsActive { get; set; } }
public sealed class UpdatePvpSpiritRulesRequest { public List<PvpSpiritRuleAdminRequest> Rules { get; set; } = []; }
public sealed class PvpRankTierAdminRequest { public string TierCode { get; set; } = null!; public string DisplayName { get; set; } = null!; public int MinMmr { get; set; } public short SortOrder { get; set; } public string AssetKey { get; set; } = null!; public string ColorHex { get; set; } = null!; public bool IsActive { get; set; } }
public sealed class UpdatePvpRankTiersRequest { public List<PvpRankTierAdminRequest> Tiers { get; set; } = []; }
