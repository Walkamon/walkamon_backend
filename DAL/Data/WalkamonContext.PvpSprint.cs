using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Data;

public partial class WalkamonContext
{
    public DbSet<PvpPlayerProfile> PvpPlayerProfiles => Set<PvpPlayerProfile>();
    public DbSet<PvpPlayerActivity> PvpPlayerActivities => Set<PvpPlayerActivity>();
    public DbSet<PvpBotProfile> PvpBotProfiles => Set<PvpBotProfile>();
    public DbSet<PvpSprintInvite> PvpSprintInvites => Set<PvpSprintInvite>();
    public DbSet<PvpStepSession> PvpStepSessions => Set<PvpStepSession>();
    public DbSet<StepSensorBatch> StepSensorBatches => Set<StepSensorBatch>();
    public DbSet<StepCounterEvidenceSample> StepCounterEvidenceSamples => Set<StepCounterEvidenceSample>();
    public DbSet<StepMotionEvidenceWindow> StepMotionEvidenceWindows => Set<StepMotionEvidenceWindow>();
    public DbSet<ValidatedStepRecord> ValidatedStepRecords => Set<ValidatedStepRecord>();
    public DbSet<PvpRewardRule> PvpRewardRules => Set<PvpRewardRule>();
    public DbSet<PvpMatchRewardEntitlement> PvpMatchRewardEntitlements => Set<PvpMatchRewardEntitlement>();
    public DbSet<PvpMatchRewardItem> PvpMatchRewardItems => Set<PvpMatchRewardItem>();
    public DbSet<PvpMatchRewardSnapshot> PvpMatchRewardSnapshots => Set<PvpMatchRewardSnapshot>();
    public DbSet<PvpMatchRewardSnapshotItem> PvpMatchRewardSnapshotItems => Set<PvpMatchRewardSnapshotItem>();
    public DbSet<PvpMatchEvent> PvpMatchEvents => Set<PvpMatchEvent>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<PvpItemEffectDefinition> PvpItemEffectDefinitions => Set<PvpItemEffectDefinition>();
    public DbSet<PvpPlayerLoadoutSlot> PvpPlayerLoadoutSlots => Set<PvpPlayerLoadoutSlot>();
    public DbSet<PvpBotLoadoutSlot> PvpBotLoadoutSlots => Set<PvpBotLoadoutSlot>();
    public DbSet<PvpMatchLoadoutSlot> PvpMatchLoadoutSlots => Set<PvpMatchLoadoutSlot>();
    public DbSet<PvpMatchItemAction> PvpMatchItemActions => Set<PvpMatchItemAction>();
    public DbSet<PvpMatchEffect> PvpMatchEffects => Set<PvpMatchEffect>();
    public DbSet<PvpSpiritSpeedRule> PvpSpiritSpeedRules => Set<PvpSpiritSpeedRule>();
    public DbSet<PvpRankTier> PvpRankTiers => Set<PvpRankTier>();
    public DbSet<PvpMatchmakingPolicy> PvpMatchmakingPolicies => Set<PvpMatchmakingPolicy>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyStep>().Property(x => x.EligibleStepCount).HasColumnName("eligible_step_count").HasDefaultValue(0);
        modelBuilder.Entity<Pet>().Property(x => x.PvpAffinityCode).HasColumnName("pvp_affinity_code").HasMaxLength(30).IsUnicode(false);

        modelBuilder.Entity<PvpMatch>(entity =>
        {
            entity.Property(x => x.SourceCode).HasColumnName("source_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.FinishReasonCode).HasColumnName("finish_reason_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.ForfeitedByUserId).HasColumnName("forfeited_by_user_id");
            entity.Property(x => x.CountdownEndsAt).HasColumnName("countdown_ends_at").HasPrecision(0);
            entity.Property(x => x.SettlementEndsAt).HasColumnName("settlement_ends_at").HasPrecision(0);
            entity.Property(x => x.ResolvedAt).HasColumnName("resolved_at").HasPrecision(0);
            entity.Property(x => x.RatingK).HasColumnName("rating_k").HasDefaultValue(32);
            entity.Property(x => x.RatingDivisor).HasColumnName("rating_divisor").HasDefaultValue(400);
            entity.Property(x => x.SpeedMinBps).HasColumnName("speed_min_bps").HasDefaultValue(7500);
            entity.Property(x => x.SpeedMaxBps).HasColumnName("speed_max_bps").HasDefaultValue(12500);
            entity.Property(x => x.ItemSlotLimit).HasColumnName("item_slot_limit").HasDefaultValue((byte)2);
            entity.Property(x => x.RuleVersion).HasColumnName("rule_version").HasDefaultValue(1);
            entity.Property(x => x.ScoringModeCode).HasColumnName("scoring_mode_code").HasMaxLength(30).IsUnicode(false).HasDefaultValue("daily_power_v1");
            entity.Property(x => x.DailyStepPowerCap).HasColumnName("daily_step_power_cap").HasDefaultValue(10000);
            entity.Property(x => x.BasePaceMinMilliStepsPerSecond).HasColumnName("base_pace_min_milli_steps_per_second").HasDefaultValue(1000);
            entity.Property(x => x.BasePaceMaxMilliStepsPerSecond).HasColumnName("base_pace_max_milli_steps_per_second").HasDefaultValue(2500);
            entity.Property(x => x.MatchDurationSeconds).HasColumnName("match_duration_seconds").HasDefaultValue((byte)30);
            entity.Property(x => x.LastProgressAt).HasColumnName("last_progress_at").HasPrecision(3);
            entity.Property(x => x.LastEventSequence).HasColumnName("last_event_sequence").HasDefaultValue(0L);
            entity.Property(x => x.MatchmakingPolicyVersion).HasColumnName("matchmaking_policy_version");
            entity.Property(x => x.MatchmakingReasonCode).HasColumnName("matchmaking_reason_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.BotDifficultyCode).HasColumnName("bot_difficulty_code").HasMaxLength(10).IsUnicode(false);
            entity.Property(x => x.IsReliefMatch).HasColumnName("is_relief_match").HasDefaultValue(false);
            entity.Property(x => x.RatingPolicyCode).HasColumnName("rating_policy_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.SelectionRollBps).HasColumnName("selection_roll_bps");
            entity.Property(x => x.ExpectedFirstDistanceUnits).HasColumnName("expected_first_distance_units");
            entity.Property(x => x.ExpectedSecondDistanceUnits).HasColumnName("expected_second_distance_units");
            entity.Property(x => x.ExpectedGapBps).HasColumnName("expected_gap_bps");
            entity.Property(x => x.BotRewardMultiplierBps).HasColumnName("bot_reward_multiplier_bps").ValueGeneratedNever();
            entity.Property(x => x.BotWinMmrDelta).HasColumnName("bot_win_mmr_delta");
            entity.Property(x => x.BotDrawMmrDelta).HasColumnName("bot_draw_mmr_delta");
            entity.Property(x => x.BotLossMmrDelta).HasColumnName("bot_loss_mmr_delta");
            entity.Property(x => x.BotRatingWindow).HasColumnName("bot_rating_window").HasDefaultValue((byte)20);
            entity.Property(x => x.MaxPositiveBotMmrInWindow).HasColumnName("max_positive_bot_mmr_in_window").HasDefaultValue((short)8);
            entity.Property(x => x.ProfileStateAppliedAt).HasColumnName("profile_state_applied_at").HasPrecision(0);
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            entity.HasOne(x => x.ForfeitedByUser).WithMany().HasForeignKey(x => x.ForfeitedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PvpMatchPlayer>(entity =>
        {
            entity.ToTable("pvp_match_players");
            entity.HasKey(x => x.MatchPlayerId);
            entity.Property(x => x.MatchPlayerId).HasColumnName("match_player_id");
            entity.Property(x => x.MatchId).HasColumnName("match_id");
            // The legacy scaffold configured { MatchId, UserId } as the key, which
            // made UserId required in EF metadata even after MatchPlayerId became
            // the real key. Bot participants intentionally have user_id = NULL.
            entity.Property(x => x.UserId).HasColumnName("user_id").IsRequired(false);
            entity.Property(x => x.BotProfileId).HasColumnName("bot_profile_id").IsRequired(false);
            entity.Property(x => x.ParticipantTypeCode).HasColumnName("participant_type_code").HasMaxLength(10).IsUnicode(false);
            entity.Property(x => x.MmrBefore).HasColumnName("mmr_before");
            entity.Property(x => x.MmrDelta).HasColumnName("mmr_delta");
            entity.Property(x => x.PetIdSnapshot).HasColumnName("pet_id_snapshot");
            entity.Property(x => x.PetNameSnapshot).HasColumnName("pet_name_snapshot").HasMaxLength(100);
            entity.Property(x => x.PetStageNoSnapshot).HasColumnName("pet_stage_no_snapshot");
            entity.Property(x => x.SpiritAffinityCode).HasColumnName("spirit_affinity_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.PassiveSpeedBps).HasColumnName("passive_speed_bps");
            entity.Property(x => x.ValidatedSteps).HasColumnName("validated_steps");
            entity.Property(x => x.DailyEligibleStepsSnapshot).HasColumnName("daily_eligible_steps_snapshot");
            entity.Property(x => x.BasePaceMilliStepsPerSecond).HasColumnName("base_pace_milli_steps_per_second").HasDefaultValue(1000);
            entity.Property(x => x.DistanceUnits).HasColumnName("distance_units");
            entity.Property(x => x.ExpectedDistanceUnits).HasColumnName("expected_distance_units");
            entity.Property(x => x.ExpectedSpeedBps).HasColumnName("expected_speed_bps");
            entity.Property(x => x.ExpectedPassiveBps).HasColumnName("expected_passive_bps");
            entity.Property(x => x.ExpectedLoadoutBps).HasColumnName("expected_loadout_bps");
            entity.Property(x => x.PassiveRuleBonusBpsSnapshot).HasColumnName("passive_rule_bonus_bps_snapshot");
            entity.Property(x => x.PassiveRuleStartMinuteSnapshot).HasColumnName("passive_rule_start_minute_snapshot");
            entity.Property(x => x.PassiveRuleEndMinuteSnapshot).HasColumnName("passive_rule_end_minute_snapshot");
            entity.Property(x => x.BotMinPaceSnapshot).HasColumnName("bot_min_pace_snapshot");
            entity.Property(x => x.BotMaxPaceSnapshot).HasColumnName("bot_max_pace_snapshot");
            entity.Property(x => x.ReadyAt).HasColumnName("ready_at").HasPrecision(3);
            entity.Property(x => x.RealtimeJoinedAt).HasColumnName("realtime_joined_at").HasPrecision(3);
            entity.Property(x => x.StreakEligibilityCode).HasColumnName("streak_eligibility_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            entity.HasIndex(x => new { x.MatchId, x.UserId }, "UX_pvp_match_players_match_user").IsUnique().HasFilter("[user_id] IS NOT NULL");
            entity.HasIndex(x => new { x.MatchId, x.BotProfileId }, "UX_pvp_match_players_match_bot").IsUnique().HasFilter("[bot_profile_id] IS NOT NULL");
            entity.HasOne(x => x.Match).WithMany(x => x.PvpMatchPlayers).HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany(x => x.PvpMatchPlayers).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.BotProfile).WithMany().HasForeignKey(x => x.BotProfileId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PvpPlayerProfile>(entity =>
        {
            entity.ToTable("pvp_player_profiles");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Mmr).HasColumnName("mmr").HasDefaultValue(1000);
            entity.Property(x => x.ConsecutiveValidRankedLosses).HasColumnName("consecutive_valid_ranked_losses").HasDefaultValue((short)0);
            entity.Property(x => x.CompletedRankedMatchesSinceRelief).HasColumnName("completed_ranked_matches_since_relief").HasDefaultValue(0);
            entity.Property(x => x.LastReliefCompletedAt).HasColumnName("last_relief_completed_at").HasPrecision(0);
            entity.Property(x => x.LastBotDifficultyCode).HasColumnName("last_bot_difficulty_code").HasMaxLength(10).IsUnicode(false);
            entity.Property(x => x.ConsecutiveHardBotCount).HasColumnName("consecutive_hard_bot_count").HasDefaultValue((byte)0);
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasPrecision(0);
            entity.HasOne(x => x.User).WithOne(x => x.PvpPlayerProfile).HasForeignKey<PvpPlayerProfile>(x => x.UserId);
        });

        modelBuilder.Entity<PvpPlayerActivity>(entity =>
        {
            entity.ToTable("pvp_player_activities");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.ActivityType).HasColumnName("activity_type").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.ActivityId).HasColumnName("activity_id");
            entity.Property(x => x.DueAt).HasColumnName("due_at").HasPrecision(0);
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(0);
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasPrecision(0);
            entity.HasIndex(x => new { x.ActivityType, x.DueAt }, "IX_pvp_player_activities_type_due");
            entity.HasOne(x => x.User).WithOne(x => x.PvpPlayerActivity).HasForeignKey<PvpPlayerActivity>(x => x.UserId);
        });

        modelBuilder.Entity<PvpBotProfile>(entity =>
        {
            entity.ToTable("pvp_bot_profiles");
            entity.HasKey(x => x.BotProfileId);
            entity.Property(x => x.BotProfileId).HasColumnName("bot_profile_id");
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(80);
            entity.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);
            entity.Property(x => x.Mmr).HasColumnName("mmr");
            entity.Property(x => x.StepsPerSecond).HasColumnName("steps_per_second").HasPrecision(5, 2);
            entity.Property(x => x.DifficultyCode).HasColumnName("difficulty_code").HasMaxLength(10).IsUnicode(false).HasDefaultValue("fair");
            entity.Property(x => x.MinPaceMilli).HasColumnName("min_pace_milli").HasDefaultValue(1000);
            entity.Property(x => x.MaxPaceMilli).HasColumnName("max_pace_milli").HasDefaultValue(2500);
            entity.Property(x => x.TargetUserWinMinBps).HasColumnName("target_user_win_min_bps").HasDefaultValue((short)4500);
            entity.Property(x => x.TargetUserWinMaxBps).HasColumnName("target_user_win_max_bps").HasDefaultValue((short)5500);
            entity.Property(x => x.ItemPowerBudgetBps).HasColumnName("item_power_budget_bps").HasDefaultValue((short)1000);
            entity.Property(x => x.ProfileVersion).HasColumnName("profile_version").HasDefaultValue(1);
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            entity.Property(x => x.SpiritAffinityCode).HasColumnName("spirit_affinity_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.PetStageNo).HasColumnName("pet_stage_no").HasDefaultValue((byte)1);
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(0);
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasPrecision(0);
            entity.HasIndex(x => new { x.IsActive, x.DifficultyCode, x.Mmr }, "IX_pvp_bot_profiles_active_difficulty_mmr");
        });

        modelBuilder.Entity<MatchmakingQueue>(entity =>
        {
            entity.Property(x => x.MmrSnapshot).HasColumnName("mmr_snapshot");
            entity.Property(x => x.DailyStepsSnapshot).HasColumnName("daily_steps_snapshot");
            entity.Property(x => x.BasePaceSnapshot).HasColumnName("base_pace_snapshot");
            entity.Property(x => x.ExpectedDistanceUnits).HasColumnName("expected_distance_units");
            entity.Property(x => x.ExpectedSpeedBps).HasColumnName("expected_speed_bps");
            entity.Property(x => x.PolicyVersion).HasColumnName("policy_version");
            entity.Property(x => x.RequiresRelief).HasColumnName("requires_relief").HasDefaultValue(false);
            entity.Property(x => x.PowerSnapshotAt).HasColumnName("power_snapshot_at").HasPrecision(0);
            entity.Property(x => x.BotFallbackAt).HasColumnName("bot_fallback_at").HasPrecision(0);
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
        });

        modelBuilder.Entity<PvpSprintInvite>(entity =>
        {
            entity.ToTable("pvp_sprint_invites");
            entity.HasKey(x => x.InviteId);
            entity.Property(x => x.InviteId).HasColumnName("invite_id");
            entity.Property(x => x.InviterUserId).HasColumnName("inviter_user_id");
            entity.Property(x => x.InviteeUserId).HasColumnName("invitee_user_id");
            entity.Property(x => x.UserLowId).HasColumnName("user_low_id");
            entity.Property(x => x.UserHighId).HasColumnName("user_high_id");
            entity.Property(x => x.StatusCode).HasColumnName("status_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasPrecision(0);
            entity.Property(x => x.RespondedAt).HasColumnName("responded_at").HasPrecision(0);
            entity.Property(x => x.MatchId).HasColumnName("match_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(0);
            entity.HasIndex(x => new { x.InviteeUserId, x.StatusCode, x.ExpiresAt }, "IX_pvp_sprint_invites_incoming");
            entity.HasOne(x => x.InviterUser).WithMany(x => x.PvpSprintInvitesSent).HasForeignKey(x => x.InviterUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.InviteeUser).WithMany(x => x.PvpSprintInvitesReceived).HasForeignKey(x => x.InviteeUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Match).WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PvpStepSession>(entity =>
        {
            entity.ToTable("pvp_step_sessions");
            entity.HasKey(x => x.StepSessionId);
            entity.Property(x => x.StepSessionId).HasColumnName("step_session_id");
            entity.Property(x => x.MatchId).HasColumnName("match_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.PurposeCode).HasColumnName("purpose_code").HasMaxLength(10).IsUnicode(false);
            entity.Property(x => x.PlatformCode).HasColumnName("platform_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.SensorModeCode).HasColumnName("sensor_mode_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ContractVersion).HasColumnName("contract_version").HasDefaultValue(2);
            entity.Property(x => x.CaptureMetadataJson).HasColumnName("capture_metadata_json");
            entity.Property(x => x.Nonce).HasColumnName("nonce").HasMaxLength(128);
            entity.Property(x => x.StatusCode).HasColumnName("status_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasPrecision(0);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(0);
            entity.Property(x => x.LastSubmittedAt).HasColumnName("last_submitted_at").HasPrecision(0);
            entity.Property(x => x.LastSequence).HasColumnName("last_sequence");
            entity.Property(x => x.LastSensorTotal).HasColumnName("last_sensor_total");
            entity.Property(x => x.LastRecordedAt).HasColumnName("last_recorded_at").HasPrecision(3);
            entity.Property(x => x.ClosedReason).HasColumnName("closed_reason").HasMaxLength(100);
            entity.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();
            entity.HasIndex(x => new { x.MatchId, x.UserId }, "UX_pvp_step_sessions_match_user").IsUnique().HasFilter("[match_id] IS NOT NULL");
            entity.HasIndex(x => x.UserId, "UX_pvp_step_sessions_active_user").IsUnique().HasFilter("[status_code] = 'active'");
            entity.HasOne(x => x.Match).WithMany().HasForeignKey(x => x.MatchId);
            entity.HasOne(x => x.User).WithMany(x => x.PvpStepSessions).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<StepSensorBatch>(entity =>
        {
            entity.ToTable("step_sensor_batches");
            entity.HasKey(x => x.StepSensorBatchId);
            entity.Property(x => x.StepSensorBatchId).HasColumnName("step_sensor_batch_id");
            entity.Property(x => x.StepSessionId).HasColumnName("step_session_id");
            entity.Property(x => x.Sequence).HasColumnName("sequence");
            entity.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsUnicode(false).IsFixedLength();
            entity.Property(x => x.AttestationStatus).HasColumnName("attestation_status").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.PackageName).HasColumnName("package_name").HasMaxLength(200);
            entity.Property(x => x.VerdictTimestamp).HasColumnName("verdict_timestamp").HasPrecision(3);
            entity.Property(x => x.VerdictJson).HasColumnName("verdict_json");
            entity.Property(x => x.EvidenceVersion).HasColumnName("evidence_version").HasDefaultValue(1);
            entity.Property(x => x.MotionScore).HasColumnName("motion_score").HasDefaultValue(0);
            entity.Property(x => x.MotionStatus).HasColumnName("motion_status").HasMaxLength(20).IsUnicode(false).HasDefaultValue("unavailable");
            entity.Property(x => x.MotionReasonsJson).HasColumnName("motion_reasons_json").HasDefaultValue("[]");
            entity.Property(x => x.DegradedEvidence).HasColumnName("degraded_evidence").HasDefaultValue(false);
            entity.Property(x => x.AcceptedSteps).HasColumnName("accepted_steps");
            entity.Property(x => x.RejectedSteps).HasColumnName("rejected_steps");
            entity.Property(x => x.SuspiciousSteps).HasColumnName("suspicious_steps");
            entity.Property(x => x.ReconciliationStatus).HasColumnName("reconciliation_status").HasMaxLength(30).IsUnicode(false).HasDefaultValue("unavailable");
            entity.Property(x => x.ReconciliationReason).HasColumnName("reconciliation_reason").HasMaxLength(200);
            entity.Property(x => x.ReceivedAt).HasColumnName("received_at").HasPrecision(3);
            entity.HasIndex(x => new { x.StepSessionId, x.Sequence }, "UX_step_sensor_batches_session_sequence").IsUnique();
            entity.HasIndex(x => new { x.StepSessionId, x.PayloadHash }, "UX_step_sensor_batches_session_hash").IsUnique();
            entity.HasOne(x => x.StepSession).WithMany(x => x.Batches).HasForeignKey(x => x.StepSessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StepCounterEvidenceSample>(entity =>
        {
            entity.ToTable("step_counter_evidence_samples");
            entity.HasKey(x => x.CounterSampleId);
            entity.Property(x => x.CounterSampleId).HasColumnName("counter_sample_id");
            entity.Property(x => x.BatchId).HasColumnName("batch_id");
            entity.Property(x => x.SampleIndex).HasColumnName("sample_index");
            entity.Property(x => x.ClientSampleId).HasColumnName("client_sample_id");
            entity.Property(x => x.BootSessionId).HasColumnName("boot_session_id");
            entity.Property(x => x.SensorElapsedRealtimeNs).HasColumnName("sensor_elapsed_realtime_ns");
            entity.Property(x => x.ObservedAt).HasColumnName("observed_at").HasPrecision(3);
            entity.Property(x => x.CounterTotal).HasColumnName("counter_total");
            entity.HasIndex(x => new { x.BatchId, x.SampleIndex }, "UX_step_counter_samples_batch_index").IsUnique();
            entity.HasIndex(x => x.ClientSampleId, "UX_step_counter_samples_client_id").IsUnique();
            entity.HasIndex(x => new { x.BootSessionId, x.SensorElapsedRealtimeNs }, "IX_step_counter_samples_boot_elapsed");
            entity.HasOne(x => x.Batch).WithMany(x => x.CounterSamples).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StepMotionEvidenceWindow>(entity =>
        {
            entity.ToTable("step_motion_evidence_windows");
            entity.HasKey(x => x.StepMotionEvidenceWindowId);
            entity.Property(x => x.StepMotionEvidenceWindowId).HasColumnName("step_motion_evidence_window_id");
            entity.Property(x => x.BatchId).HasColumnName("batch_id");
            entity.Property(x => x.WindowIndex).HasColumnName("window_index");
            entity.Property(x => x.BootSessionId).HasColumnName("boot_session_id");
            entity.Property(x => x.WindowStartElapsedRealtimeNs).HasColumnName("window_start_elapsed_realtime_ns");
            entity.Property(x => x.WindowEndElapsedRealtimeNs).HasColumnName("window_end_elapsed_realtime_ns");
            entity.Property(x => x.WindowStartedAt).HasColumnName("window_started_at").HasPrecision(3);
            entity.Property(x => x.WindowEndedAt).HasColumnName("window_ended_at").HasPrecision(3);
            entity.Property(x => x.SampleCount).HasColumnName("sample_count");
            entity.Property(x => x.AccelerometerSource).HasColumnName("accelerometer_source").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.GyroscopeAvailable).HasColumnName("gyroscope_available");
            entity.Property(x => x.ActivityAvailable).HasColumnName("activity_available");
            entity.Property(x => x.AccelerationRmsMilli).HasColumnName("acceleration_rms_milli");
            entity.Property(x => x.AccelerationPeakMilli).HasColumnName("acceleration_peak_milli");
            entity.Property(x => x.JerkRmsMilli).HasColumnName("jerk_rms_milli");
            entity.Property(x => x.GyroscopeRmsMilli).HasColumnName("gyroscope_rms_milli");
            entity.Property(x => x.GyroscopePeakMilli).HasColumnName("gyroscope_peak_milli");
            entity.Property(x => x.OrientationDeltaMilliDegrees).HasColumnName("orientation_delta_millidegrees");
            entity.Property(x => x.DominantFrequencyMilliHz).HasColumnName("dominant_frequency_millihz");
            entity.Property(x => x.PeriodicityBps).HasColumnName("periodicity_bps");
            entity.Property(x => x.GaitCycleCount).HasColumnName("gait_cycle_count");
            entity.Property(x => x.ActivityCode).HasColumnName("activity_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ActivityConfidence).HasColumnName("activity_confidence");
            entity.Property(x => x.MotionScore).HasColumnName("motion_score");
            entity.Property(x => x.Classification).HasColumnName("classification").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ReasonCodes).HasColumnName("reason_codes").HasMaxLength(500);
            entity.HasIndex(x => new { x.BatchId, x.WindowIndex }, "UX_step_motion_windows_batch_index").IsUnique();
            entity.HasIndex(
                x => new
                {
                    x.BootSessionId,
                    x.WindowStartElapsedRealtimeNs,
                    x.WindowEndElapsedRealtimeNs
                },
                "IX_step_motion_windows_boot_elapsed");
            entity.HasIndex(x => new { x.Classification, x.WindowStartedAt }, "IX_step_motion_windows_classification_started");
            entity.HasOne(x => x.Batch).WithMany(x => x.MotionWindows).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ValidatedStepRecord>(entity =>
        {
            entity.ToTable("validated_step_records");
            entity.HasKey(x => x.ValidatedStepRecordId);
            entity.Property(x => x.ValidatedStepRecordId).HasColumnName("validated_step_record_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.StepSessionId).HasColumnName("step_session_id");
            entity.Property(x => x.BatchId).HasColumnName("batch_id");
            entity.Property(x => x.EventIndex).HasColumnName("event_index");
            entity.Property(x => x.ClientEventId).HasColumnName("client_event_id");
            entity.Property(x => x.BootSessionId).HasColumnName("boot_session_id");
            entity.Property(x => x.SensorElapsedRealtimeNs).HasColumnName("sensor_elapsed_realtime_ns");
            entity.Property(x => x.PlatformCode).HasColumnName("platform_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.SourceCode).HasColumnName("source_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.SensorModeCode).HasColumnName("sensor_mode_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.IntervalStartedAt).HasColumnName("interval_started_at").HasPrecision(3);
            entity.Property(x => x.RecordedAt).HasColumnName("recorded_at").HasPrecision(3);
            entity.Property(x => x.SensorStartTotal).HasColumnName("sensor_start_total");
            entity.Property(x => x.SensorEndTotal).HasColumnName("sensor_end_total");
            entity.Property(x => x.StepCount).HasColumnName("step_count");
            entity.Property(x => x.EligibleStepCount).HasColumnName("eligible_step_count");
            entity.Property(x => x.SequenceNumber).HasColumnName("sequence_number");
            entity.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.ValidationStatus).HasColumnName("validation_status").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(200);
            entity.Property(x => x.MotionScore).HasColumnName("motion_score").HasDefaultValue(0);
            entity.Property(x => x.MotionStatus).HasColumnName("motion_status").HasMaxLength(20).IsUnicode(false).HasDefaultValue("unavailable");
            entity.Property(x => x.ReceivedAt).HasColumnName("received_at").HasPrecision(3);
            entity.HasIndex(x => new { x.UserId, x.PayloadHash }, "UX_validated_step_records_user_hash").IsUnique();
            entity.HasIndex(x => new { x.BatchId, x.EventIndex }, "UX_validated_step_records_batch_event").IsUnique().HasFilter("[batch_id] IS NOT NULL");
            entity.HasIndex(x => new { x.StepSessionId, x.ClientEventId }, "UX_validated_step_records_session_client_event").IsUnique().HasFilter("[client_event_id] IS NOT NULL");
            entity.HasOne(x => x.User).WithMany(x => x.ValidatedStepRecords).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.StepSession).WithMany().HasForeignKey(x => x.StepSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Batch).WithMany(x => x.Records).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PvpRewardRule>(entity =>
        {
            entity.ToTable("pvp_reward_rules");
            entity.HasKey(x => x.PvpRewardRuleId);
            entity.Property(x => x.PvpRewardRuleId).HasColumnName("pvp_reward_rule_id");
            entity.Property(x => x.MatchTypeCode).HasColumnName("match_type_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ResultCode).HasColumnName("result_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.RewardPackageId).HasColumnName("reward_package_id");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasPrecision(0);
            entity.HasIndex(x => new { x.MatchTypeCode, x.ResultCode }, "UX_pvp_reward_rules_type_result").IsUnique();
            entity.HasOne(x => x.RewardPackage).WithMany().HasForeignKey(x => x.RewardPackageId);
        });

        modelBuilder.Entity<PvpMatchRewardEntitlement>(entity =>
        {
            entity.ToTable("pvp_match_reward_entitlements");
            entity.HasKey(x => x.MatchRewardEntitlementId);
            entity.Property(x => x.MatchRewardEntitlementId).HasColumnName("match_reward_entitlement_id");
            entity.Property(x => x.MatchId).HasColumnName("match_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.ResultCode).HasColumnName("result_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.WalletAmount).HasColumnName("wallet_amount");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(0);
            entity.Property(x => x.ClaimedAt).HasColumnName("claimed_at").HasPrecision(0);
            entity.HasIndex(x => new { x.MatchId, x.UserId }, "UX_pvp_match_reward_entitlements_match_user").IsUnique();
            entity.HasOne(x => x.Match).WithMany(x => x.RewardEntitlements).HasForeignKey(x => x.MatchId);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<PvpMatchRewardItem>(entity =>
        {
            entity.ToTable("pvp_match_reward_items");
            entity.HasKey(x => new { x.MatchRewardEntitlementId, x.ItemId });
            entity.Property(x => x.MatchRewardEntitlementId).HasColumnName("match_reward_entitlement_id");
            entity.Property(x => x.ItemId).HasColumnName("item_id");
            entity.Property(x => x.Quantity).HasColumnName("quantity");
            entity.HasOne(x => x.Entitlement).WithMany(x => x.Items).HasForeignKey(x => x.MatchRewardEntitlementId);
            entity.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId);
        });

        modelBuilder.Entity<PvpMatchRewardSnapshot>(entity =>
        {
            entity.ToTable("pvp_match_reward_snapshots");
            entity.HasKey(x => x.MatchRewardSnapshotId);
            entity.Property(x => x.MatchRewardSnapshotId).HasColumnName("match_reward_snapshot_id");
            entity.Property(x => x.MatchId).HasColumnName("match_id");
            entity.Property(x => x.ResultCode).HasColumnName("result_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.WalletAmount).HasColumnName("wallet_amount");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(0);
            entity.HasIndex(x => new { x.MatchId, x.ResultCode }, "UX_pvp_match_reward_snapshots_match_result").IsUnique();
            entity.HasOne(x => x.Match).WithMany(x => x.RewardSnapshots).HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PvpMatchRewardSnapshotItem>(entity =>
        {
            entity.ToTable("pvp_match_reward_snapshot_items");
            entity.HasKey(x => new { x.MatchRewardSnapshotId, x.ItemId });
            entity.Property(x => x.MatchRewardSnapshotId).HasColumnName("match_reward_snapshot_id");
            entity.Property(x => x.ItemId).HasColumnName("item_id");
            entity.Property(x => x.Quantity).HasColumnName("quantity");
            entity.HasOne(x => x.Snapshot).WithMany(x => x.Items).HasForeignKey(x => x.MatchRewardSnapshotId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PvpMatchEvent>(entity =>
        {
            entity.ToTable("pvp_match_events");
            entity.HasKey(x => x.PvpMatchEventId);
            entity.Property(x => x.PvpMatchEventId).HasColumnName("pvp_match_event_id");
            entity.Property(x => x.MatchId).HasColumnName("match_id");
            entity.Property(x => x.Sequence).HasColumnName("sequence");
            entity.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.PayloadJson).HasColumnName("payload_json");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(3);
            entity.HasIndex(x => new { x.MatchId, x.Sequence }, "UX_pvp_match_events_match_sequence").IsUnique();
            entity.HasOne(x => x.Match).WithMany(x => x.Events).HasForeignKey(x => x.MatchId);
        });

        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.ToTable("outbox_events");
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventId).HasColumnName("event_id");
            entity.Property(x => x.AggregateType).HasColumnName("aggregate_type").HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.AggregateId).HasColumnName("aggregate_id");
            entity.Property(x => x.Destination).HasColumnName("destination").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.PayloadJson).HasColumnName("payload_json");
            entity.Property(x => x.Attempts).HasColumnName("attempts");
            entity.Property(x => x.LeaseUntil).HasColumnName("lease_until").HasPrecision(3);
            entity.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(100);
            entity.Property(x => x.PublishedAt).HasColumnName("published_at").HasPrecision(3);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(3);
            entity.HasIndex(x => new { x.PublishedAt, x.LeaseUntil }, "IX_outbox_events_dispatch");
        });

        modelBuilder.Entity<PvpItemEffectDefinition>(entity =>
        {
            entity.ToTable("pvp_item_effect_definitions");
            entity.HasKey(x => x.PvpItemEffectDefinitionId);
            entity.Property(x => x.PvpItemEffectDefinitionId).HasColumnName("pvp_item_effect_definition_id");
            entity.Property(x => x.ItemId).HasColumnName("item_id");
            entity.Property(x => x.EffectCode).HasColumnName("effect_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.TargetCode).HasColumnName("target_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.MagnitudeBps).HasColumnName("magnitude_bps");
            entity.Property(x => x.DurationMs).HasColumnName("duration_ms");
            entity.Property(x => x.CooldownMs).HasColumnName("cooldown_ms");
            entity.Property(x => x.AssetKey).HasColumnName("asset_key").HasMaxLength(300);
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasPrecision(0);
            entity.HasIndex(x => x.ItemId).IsUnique();
            entity.HasIndex(x => x.EffectCode).IsUnique();
            entity.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PvpPlayerLoadoutSlot>(entity =>
        {
            entity.ToTable("pvp_player_loadout_slots");
            entity.HasKey(x => new { x.UserId, x.SlotNo });
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.SlotNo).HasColumnName("slot_no");
            entity.Property(x => x.ItemId).HasColumnName("item_id");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasPrecision(0);
            entity.HasIndex(x => new { x.UserId, x.ItemId }).IsUnique();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PvpBotLoadoutSlot>(entity =>
        {
            entity.ToTable("pvp_bot_loadout_slots");
            entity.HasKey(x => new { x.BotProfileId, x.SlotNo });
            entity.Property(x => x.BotProfileId).HasColumnName("bot_profile_id");
            entity.Property(x => x.SlotNo).HasColumnName("slot_no");
            entity.Property(x => x.ItemId).HasColumnName("item_id");
            entity.HasIndex(x => new { x.BotProfileId, x.ItemId }).IsUnique();
            entity.HasOne(x => x.BotProfile).WithMany().HasForeignKey(x => x.BotProfileId);
            entity.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PvpMatchLoadoutSlot>(entity =>
        {
            entity.ToTable("pvp_match_loadout_slots");
            entity.HasKey(x => x.PvpMatchLoadoutSlotId);
            entity.Property(x => x.PvpMatchLoadoutSlotId).HasColumnName("pvp_match_loadout_slot_id");
            entity.Property(x => x.MatchId).HasColumnName("match_id");
            entity.Property(x => x.MatchPlayerId).HasColumnName("match_player_id");
            entity.Property(x => x.SlotNo).HasColumnName("slot_no");
            entity.Property(x => x.ItemId).HasColumnName("item_id");
            entity.Property(x => x.EffectCode).HasColumnName("effect_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.TargetCode).HasColumnName("target_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.MagnitudeBps).HasColumnName("magnitude_bps");
            entity.Property(x => x.DurationMs).HasColumnName("duration_ms");
            entity.Property(x => x.CooldownMs).HasColumnName("cooldown_ms");
            entity.Property(x => x.AssetKey).HasColumnName("asset_key").HasMaxLength(300);
            entity.Property(x => x.UsedAt).HasColumnName("used_at").HasPrecision(3);
            entity.HasIndex(x => new { x.MatchPlayerId, x.SlotNo }).IsUnique();
            entity.HasOne(x => x.Match).WithMany(x => x.LoadoutSlots).HasForeignKey(x => x.MatchId);
            entity.HasOne(x => x.MatchPlayer).WithMany(x => x.LoadoutSlots).HasForeignKey(x => x.MatchPlayerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PvpMatchItemAction>(entity =>
        {
            entity.ToTable("pvp_match_item_actions");
            entity.HasKey(x => x.PvpMatchItemActionId);
            entity.Property(x => x.PvpMatchItemActionId).HasColumnName("pvp_match_item_action_id");
            entity.Property(x => x.MatchId).HasColumnName("match_id");
            entity.Property(x => x.ActorMatchPlayerId).HasColumnName("actor_match_player_id");
            entity.Property(x => x.TargetMatchPlayerId).HasColumnName("target_match_player_id");
            entity.Property(x => x.MatchLoadoutSlotId).HasColumnName("match_loadout_slot_id");
            entity.Property(x => x.ClientActionId).HasColumnName("client_action_id");
            entity.Property(x => x.ResultCode).HasColumnName("result_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.EffectCode).HasColumnName("effect_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(3);
            entity.HasIndex(x => new { x.ActorMatchPlayerId, x.ClientActionId }).IsUnique();
            entity.HasOne(x => x.Match).WithMany().HasForeignKey(x => x.MatchId);
            entity.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorMatchPlayerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Target).WithMany().HasForeignKey(x => x.TargetMatchPlayerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.MatchLoadoutSlot).WithMany().HasForeignKey(x => x.MatchLoadoutSlotId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PvpMatchEffect>(entity =>
        {
            entity.ToTable("pvp_match_effects");
            entity.HasKey(x => x.PvpMatchEffectId);
            entity.Property(x => x.PvpMatchEffectId).HasColumnName("pvp_match_effect_id");
            entity.Property(x => x.MatchId).HasColumnName("match_id");
            entity.Property(x => x.TargetMatchPlayerId).HasColumnName("target_match_player_id");
            entity.Property(x => x.SourceMatchPlayerId).HasColumnName("source_match_player_id");
            entity.Property(x => x.SourceItemActionId).HasColumnName("source_item_action_id");
            entity.Property(x => x.EffectCode).HasColumnName("effect_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.EffectKindCode).HasColumnName("effect_kind_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.MagnitudeBps).HasColumnName("magnitude_bps");
            entity.Property(x => x.StatusCode).HasColumnName("status_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.StartsAt).HasColumnName("starts_at").HasPrecision(3);
            entity.Property(x => x.EndsAt).HasColumnName("ends_at").HasPrecision(3);
            entity.Property(x => x.ConsumedAt).HasColumnName("consumed_at").HasPrecision(3);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(3);
            entity.HasIndex(x => new { x.MatchId, x.TargetMatchPlayerId, x.StatusCode, x.EndsAt });
            entity.HasOne(x => x.Match).WithMany(x => x.Effects).HasForeignKey(x => x.MatchId);
            entity.HasOne(x => x.Target).WithMany(x => x.EffectsReceived).HasForeignKey(x => x.TargetMatchPlayerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SourceItemAction)
                .WithMany(x => x.Effects)
                .HasForeignKey(x => x.SourceItemActionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PvpSpiritSpeedRule>(entity =>
        {
            entity.ToTable("pvp_spirit_speed_rules");
            entity.HasKey(x => x.AffinityCode);
            entity.Property(x => x.AffinityCode).HasColumnName("affinity_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.StartMinute).HasColumnName("start_minute");
            entity.Property(x => x.EndMinute).HasColumnName("end_minute");
            entity.Property(x => x.BonusBps).HasColumnName("bonus_bps");
            entity.Property(x => x.TimeZoneCode).HasColumnName("time_zone_code").HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasPrecision(0);
        });

        modelBuilder.Entity<PvpRankTier>(entity =>
        {
            entity.ToTable("pvp_rank_tiers");
            entity.HasKey(x => x.TierCode);
            entity.Property(x => x.TierCode).HasColumnName("tier_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(80);
            entity.Property(x => x.MinMmr).HasColumnName("min_mmr");
            entity.Property(x => x.SortOrder).HasColumnName("sort_order");
            entity.Property(x => x.AssetKey).HasColumnName("asset_key").HasMaxLength(300);
            entity.Property(x => x.ColorHex).HasColumnName("color_hex").HasMaxLength(7).IsUnicode(false);
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.HasIndex(x => x.MinMmr).IsUnique();
        });

        modelBuilder.Entity<PvpMatchmakingPolicy>(entity =>
        {
            entity.ToTable("pvp_matchmaking_policies");
            entity.HasKey(x => x.PolicyVersion);
            entity.Property(x => x.PolicyVersion).HasColumnName("policy_version").ValueGeneratedNever();
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.MatchDurationSeconds).HasColumnName("match_duration_seconds");
            entity.Property(x => x.BotFallbackSeconds).HasColumnName("bot_fallback_seconds");
            entity.Property(x => x.Stage1MmrGap).HasColumnName("stage1_mmr_gap");
            entity.Property(x => x.Stage1PowerGapBps).HasColumnName("stage1_power_gap_bps");
            entity.Property(x => x.Stage1PaceRatioBps).HasColumnName("stage1_pace_ratio_bps");
            entity.Property(x => x.Stage2MmrGap).HasColumnName("stage2_mmr_gap");
            entity.Property(x => x.Stage2PowerGapBps).HasColumnName("stage2_power_gap_bps");
            entity.Property(x => x.Stage2PaceRatioBps).HasColumnName("stage2_pace_ratio_bps");
            entity.Property(x => x.Stage3MmrGap).HasColumnName("stage3_mmr_gap");
            entity.Property(x => x.Stage3PowerGapBps).HasColumnName("stage3_power_gap_bps");
            entity.Property(x => x.Stage3PaceRatioBps).HasColumnName("stage3_pace_ratio_bps");
            entity.Property(x => x.HardMmrGap).HasColumnName("hard_mmr_gap");
            entity.Property(x => x.HardPowerGapBps).HasColumnName("hard_power_gap_bps");
            entity.Property(x => x.HardPaceRatioBps).HasColumnName("hard_pace_ratio_bps");
            entity.Property(x => x.Streak01EasyWeightBps).HasColumnName("streak01_easy_weight_bps");
            entity.Property(x => x.Streak01FairWeightBps).HasColumnName("streak01_fair_weight_bps");
            entity.Property(x => x.Streak01HardWeightBps).HasColumnName("streak01_hard_weight_bps");
            entity.Property(x => x.Streak23EasyWeightBps).HasColumnName("streak23_easy_weight_bps");
            entity.Property(x => x.Streak23FairWeightBps).HasColumnName("streak23_fair_weight_bps");
            entity.Property(x => x.Streak23HardWeightBps).HasColumnName("streak23_hard_weight_bps");
            entity.Property(x => x.Streak4EasyWeightBps).HasColumnName("streak4_easy_weight_bps");
            entity.Property(x => x.Streak4FairWeightBps).HasColumnName("streak4_fair_weight_bps");
            entity.Property(x => x.Streak4HardWeightBps).HasColumnName("streak4_hard_weight_bps");
            entity.Property(x => x.ReliefLossThreshold).HasColumnName("relief_loss_threshold");
            entity.Property(x => x.ReliefTargetUserWinBps).HasColumnName("relief_target_user_win_bps");
            entity.Property(x => x.EasyTargetUserWinBps).HasColumnName("easy_target_user_win_bps");
            entity.Property(x => x.FairTargetUserWinBps).HasColumnName("fair_target_user_win_bps");
            entity.Property(x => x.HardTargetUserWinBps).HasColumnName("hard_target_user_win_bps");
            entity.Property(x => x.BotHistoryWindow).HasColumnName("bot_history_window");
            entity.Property(x => x.MaxBotMatchesInWindow).HasColumnName("max_bot_matches_in_window");
            entity.Property(x => x.AllowConsecutiveHard).HasColumnName("allow_consecutive_hard");
            entity.Property(x => x.EasyWinMmrDelta).HasColumnName("easy_win_mmr_delta");
            entity.Property(x => x.EasyDrawMmrDelta).HasColumnName("easy_draw_mmr_delta");
            entity.Property(x => x.EasyLossMmrDelta).HasColumnName("easy_loss_mmr_delta");
            entity.Property(x => x.FairWinMmrDelta).HasColumnName("fair_win_mmr_delta");
            entity.Property(x => x.FairDrawMmrDelta).HasColumnName("fair_draw_mmr_delta");
            entity.Property(x => x.FairLossMmrDelta).HasColumnName("fair_loss_mmr_delta");
            entity.Property(x => x.HardWinMmrDelta).HasColumnName("hard_win_mmr_delta");
            entity.Property(x => x.HardDrawMmrDelta).HasColumnName("hard_draw_mmr_delta");
            entity.Property(x => x.HardLossMmrDelta).HasColumnName("hard_loss_mmr_delta");
            entity.Property(x => x.ReliefWinMmrDelta).HasColumnName("relief_win_mmr_delta");
            entity.Property(x => x.ReliefDrawMmrDelta).HasColumnName("relief_draw_mmr_delta");
            entity.Property(x => x.ReliefLossMmrDelta).HasColumnName("relief_loss_mmr_delta");
            entity.Property(x => x.BotRatingWindow).HasColumnName("bot_rating_window");
            entity.Property(x => x.MaxPositiveBotMmrInWindow).HasColumnName("max_positive_bot_mmr_in_window");
            entity.Property(x => x.EasyRewardMultiplierBps).HasColumnName("easy_reward_multiplier_bps");
            entity.Property(x => x.FairRewardMultiplierBps).HasColumnName("fair_reward_multiplier_bps");
            entity.Property(x => x.HardRewardMultiplierBps).HasColumnName("hard_reward_multiplier_bps");
            entity.Property(x => x.ReliefRewardMultiplierBps).HasColumnName("relief_reward_multiplier_bps");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(0);
            entity.Property(x => x.ActivatedAt).HasColumnName("activated_at").HasPrecision(0);
            entity.HasIndex(x => x.IsActive, "UX_pvp_matchmaking_policies_active").IsUnique().HasFilter("[is_active] = 1");
        });
    }
}
