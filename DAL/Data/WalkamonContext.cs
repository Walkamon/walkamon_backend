using System;
using System.Collections.Generic;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Data;

public partial class WalkamonContext : DbContext
{
    public WalkamonContext()
    {
    }

    public WalkamonContext(DbContextOptions<WalkamonContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Achievement> Achievements { get; set; }

    public virtual DbSet<DailyStep> DailySteps { get; set; }

    public virtual DbSet<DeviceToken> DeviceTokens { get; set; }

    public virtual DbSet<ExternalLogin> ExternalLogins { get; set; }

    public virtual DbSet<FriendRequest> FriendRequests { get; set; }

    public virtual DbSet<Friendship> Friendships { get; set; }

    public virtual DbSet<InventoryItem> InventoryItems { get; set; }

    public virtual DbSet<Item> Items { get; set; }

    public virtual DbSet<ItemType> ItemTypes { get; set; }

    public virtual DbSet<MatchmakingQueue> MatchmakingQueues { get; set; }

    public virtual DbSet<Mission> Missions { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OtpRequest> OtpRequests { get; set; }

    public virtual DbSet<Pet> Pets { get; set; }

    public virtual DbSet<PetEvolutionHistory> PetEvolutionHistories { get; set; }

    public virtual DbSet<PetLevel> PetLevels { get; set; }

    public virtual DbSet<PetSpecy> PetSpecies { get; set; }

    public virtual DbSet<PetStage> PetStages { get; set; }

    public virtual DbSet<PvpMatch> PvpMatches { get; set; }

    public virtual DbSet<PvpMatchPlayer> PvpMatchPlayers { get; set; }

    public virtual DbSet<RewardPackage> RewardPackages { get; set; }

    public virtual DbSet<RewardPackageItem> RewardPackageItems { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<ShopItem> ShopItems { get; set; }

    public virtual DbSet<ShopPurchase> ShopPurchases { get; set; }

    public virtual DbSet<StepGoal> StepGoals { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAchievement> UserAchievements { get; set; }

    public virtual DbSet<UserMission> UserMissions { get; set; }

    public virtual DbSet<UserNotification> UserNotifications { get; set; }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    public virtual DbSet<Wallet> Wallets { get; set; }

  

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.HasKey(e => e.AchievementId).HasName("PK__achievem__3C492E83D5F50658");

            entity.ToTable("achievements");

            entity.HasIndex(e => e.Title, "UQ__achievem__E52A1BB3441D14B5").IsUnique();

            entity.Property(e => e.AchievementId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("achievement_id");
            entity.Property(e => e.CategoryCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("category_code");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.MetricCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("metric_code");
            entity.Property(e => e.RewardPackageId).HasColumnName("reward_package_id");
            entity.Property(e => e.TargetValue).HasColumnName("target_value");
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .HasColumnName("title");

            entity.HasOne(d => d.RewardPackage).WithMany(p => p.Achievements)
                .HasForeignKey(d => d.RewardPackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__achieveme__rewar__46B27FE2");
        });

        modelBuilder.Entity<DailyStep>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StepDate }).HasName("PK__daily_st__37CD0C07C8107828");

            entity.ToTable("daily_steps");

            entity.HasIndex(e => new { e.StepDate, e.StepCount }, "IX_daily_steps_step_date_step_count").IsDescending(false, true);

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.StepDate).HasColumnName("step_date");
            entity.Property(e => e.StepCount).HasColumnName("step_count");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.User).WithMany(p => p.DailySteps)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__daily_ste__user___29221CFB");
        });

        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.HasKey(e => e.DeviceTokenId).HasName("PK__device_t__3ADABB7D2799A8FB");

            entity.ToTable("device_tokens");

            entity.HasIndex(e => new { e.UserId, e.IsActive }, "IX_device_tokens_user_active");

            entity.HasIndex(e => e.FcmToken, "UQ__device_t__61C3D35D737AEB17").IsUnique();

            entity.Property(e => e.DeviceTokenId).HasColumnName("device_token_id");
            entity.Property(e => e.FcmToken)
                .HasMaxLength(512)
                .HasColumnName("fcm_token");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.DeviceTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__device_to__user___6754599E");
        });

        modelBuilder.Entity<ExternalLogin>(entity =>
        {
            entity.HasKey(e => e.ExternalLoginId).HasName("PK__external__71E083FBADBC42F7");

            entity.ToTable("external_logins");

            entity.HasIndex(e => e.UserId, "IX_external_logins_user_id");

            entity.HasIndex(e => new { e.ProviderName, e.ProviderSubject }, "UQ__external__8B7190E17F6F2F99").IsUnique();

            entity.Property(e => e.ExternalLoginId).HasColumnName("external_login_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.LastLoginAt)
                .HasPrecision(0)
                .HasColumnName("last_login_at");
            entity.Property(e => e.ProviderDisplayName)
                .HasMaxLength(200)
                .HasColumnName("provider_display_name");
            entity.Property(e => e.ProviderEmail)
                .HasMaxLength(320)
                .HasColumnName("provider_email");
            entity.Property(e => e.ProviderName)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("provider_name");
            entity.Property(e => e.ProviderSubject)
                .HasMaxLength(200)
                .HasColumnName("provider_subject");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.ExternalLogins)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__external___user___48CFD27E");
        });

        modelBuilder.Entity<FriendRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__friend_r__18D3B90FCD0CE5CC");

            entity.ToTable("friend_requests");

            entity.HasIndex(e => new { e.ReceiverUserId, e.StatusCode, e.CreatedAt }, "IX_friend_requests_receiver_status_created").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.SenderUserId, e.ReceiverUserId }, "UX_friend_requests_pending_sender_receiver")
                .IsUnique()
                .HasFilter("([status_code]='pending')");

            entity.Property(e => e.RequestId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("request_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.ReceiverUserId).HasColumnName("receiver_user_id");
            entity.Property(e => e.RespondedAt)
                .HasPrecision(0)
                .HasColumnName("responded_at");
            entity.Property(e => e.SenderUserId).HasColumnName("sender_user_id");
            entity.Property(e => e.StatusCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("pending")
                .HasColumnName("status_code");

            entity.HasOne(d => d.ReceiverUser).WithMany(p => p.FriendRequestReceiverUsers)
                .HasForeignKey(d => d.ReceiverUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__friend_re__recei__6BE40491");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.FriendRequestSenderUsers)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__friend_re__sende__6AEFE058");
        });

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => new { e.UserLowId, e.UserHighId }).HasName("PK__friendsh__4846FE8BE69B8C08");

            entity.ToTable("friendships");

            entity.Property(e => e.UserLowId).HasColumnName("user_low_id");
            entity.Property(e => e.UserHighId).HasColumnName("user_high_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");

            entity.HasOne(d => d.UserHigh).WithMany(p => p.FriendshipUserHighs)
                .HasForeignKey(d => d.UserHighId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__friendshi__user___719CDDE7");

            entity.HasOne(d => d.UserLow).WithMany(p => p.FriendshipUserLows)
                .HasForeignKey(d => d.UserLowId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__friendshi__user___70A8B9AE");
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ItemId }).HasName("PK__inventor__7C9E17F2F47D55E9");

            entity.ToTable("inventory_items");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.Item).WithMany(p => p.InventoryItems)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__inventory__item___5224328E");

            entity.HasOne(d => d.User).WithMany(p => p.InventoryItems)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__inventory__user___51300E55");
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("PK__items__52020FDD58D88264");

            entity.ToTable("items");

            entity.HasIndex(e => e.ItemName, "UQ__items__ACA52A97595374DB").IsUnique();

            entity.Property(e => e.ItemId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("item_id");
            entity.Property(e => e.Description)
                .HasMaxLength(300)
                .HasColumnName("description");
            entity.Property(e => e.EffectTypeCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("effect_type_code");
            entity.Property(e => e.EffectValue).HasColumnName("effect_value");
            entity.Property(e => e.ImgUrl)
                .HasMaxLength(300)
                .HasColumnName("img_url");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ItemName)
                .HasMaxLength(80)
                .HasColumnName("item_name");
            entity.Property(e => e.ItemTypeId).HasColumnName("item_type_id");

            entity.HasOne(d => d.ItemType).WithMany(p => p.Items)
                .HasForeignKey(d => d.ItemTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__items__item_type__787EE5A0");
        });

        modelBuilder.Entity<ItemType>(entity =>
        {
            entity.HasKey(e => e.ItemTypeId).HasName("PK__item_typ__470682AB80E562B8");

            entity.ToTable("item_types");

            entity.HasIndex(e => e.ItemTypeName, "UQ__item_typ__2034EA22024349A2").IsUnique();

            entity.Property(e => e.ItemTypeId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("item_type_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.ItemTypeName)
                .HasMaxLength(80)
                .HasColumnName("item_type_name");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<MatchmakingQueue>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__matchmak__B9BE370F6FAA0008");

            entity.ToTable("matchmaking_queue");

            entity.HasIndex(e => new { e.StatusCode, e.MatchTypeCode, e.QueuedAt }, "IX_matchmaking_queue_status_type_time");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.MatchTypeCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ranked")
                .HasColumnName("match_type_code");
            entity.Property(e => e.PetLevelSnapshot).HasColumnName("pet_level_snapshot");
            entity.Property(e => e.QueuedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("queued_at");
            entity.Property(e => e.StatusCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("waiting")
                .HasColumnName("status_code");

            entity.HasOne(d => d.User).WithOne(p => p.MatchmakingQueue)
                .HasForeignKey<MatchmakingQueue>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__matchmaki__user___0697FACD");
        });

        modelBuilder.Entity<Mission>(entity =>
        {
            entity.HasKey(e => e.MissionId).HasName("PK__missions__B5419AB235A6599E");

            entity.ToTable("missions");

            entity.HasIndex(e => new { e.MissionTypeCode, e.IsActive, e.StartAt, e.EndAt }, "IX_missions_type_active_window");

            entity.Property(e => e.MissionId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("mission_id");
            entity.Property(e => e.EndAt)
                .HasPrecision(0)
                .HasColumnName("end_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsCancelable).HasColumnName("is_cancelable");
            entity.Property(e => e.MetricCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("metric_code");
            entity.Property(e => e.MissionTypeCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("mission_type_code");
            entity.Property(e => e.RewardPackageId).HasColumnName("reward_package_id");
            entity.Property(e => e.StartAt)
                .HasPrecision(0)
                .HasColumnName("start_at");
            entity.Property(e => e.TargetValue).HasColumnName("target_value");
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .HasColumnName("title");

            entity.HasOne(d => d.RewardPackage).WithMany(p => p.Missions)
                .HasForeignKey(d => d.RewardPackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__missions__reward__3587F3E0");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__notifica__E059842F2C76C317");

            entity.ToTable("notifications");

            entity.HasIndex(e => new { e.ScheduledAt, e.CreatedAt }, "IX_notifications_schedule").IsDescending(false, true);

            entity.Property(e => e.NotificationId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("notification_id");
            entity.Property(e => e.Body)
                .HasMaxLength(500)
                .HasColumnName("body");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.NotificationTypeCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("notification_type_code");
            entity.Property(e => e.ScheduledAt)
                .HasPrecision(0)
                .HasColumnName("scheduled_at");
            entity.Property(e => e.Title)
                .HasMaxLength(120)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("FK__notificat__creat__7755B73D");
        });

        modelBuilder.Entity<OtpRequest>(entity =>
        {
            entity.HasKey(e => e.OtpRequestId).HasName("PK__otp_requ__638D23F5C3770348");

            entity.ToTable("otp_requests");

            entity.HasIndex(e => new { e.UserId, e.PurposeCode, e.StatusCode, e.ExpiresAt }, "IX_otp_requests_user_purpose_status");

            entity.HasIndex(e => e.RequestCode, "UQ_otp_requests_request_code").IsUnique();

            entity.HasIndex(e => e.UserId, "UX_otp_requests_verify_email_pending_user")
                .IsUnique()
                .HasFilter("([purpose_code]='verify_email' AND [status_code]='pending')");

            entity.Property(e => e.OtpRequestId).HasColumnName("otp_request_id");
            entity.Property(e => e.AttemptCount).HasColumnName("attempt_count");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt)
                .HasPrecision(0)
                .HasColumnName("expires_at");
            entity.Property(e => e.MaxAttempts)
                .HasDefaultValue((short)5)
                .HasColumnName("max_attempts");
            entity.Property(e => e.OtpHash)
                .HasMaxLength(32)
                .IsFixedLength()
                .HasColumnName("otp_hash");
            entity.Property(e => e.PurposeCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("purpose_code");
            entity.Property(e => e.RequestCode)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("request_code");
            entity.Property(e => e.RequestedIp)
                .HasMaxLength(45)
                .IsUnicode(false)
                .HasColumnName("requested_ip");
            entity.Property(e => e.StatusCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("pending")
                .HasColumnName("status_code");
            entity.Property(e => e.TargetValue)
                .HasMaxLength(320)
                .HasColumnName("target_value");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UsedAt)
                .HasPrecision(0)
                .HasColumnName("used_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithOne(p => p.OtpRequest)
                .HasForeignKey<OtpRequest>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__otp_reque__user___5629CD9C");
        });

        modelBuilder.Entity<Pet>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__pets__B9BE370F9C5FB275");

            entity.ToTable("pets");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.Bond).HasColumnName("bond");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CurrentStageId).HasColumnName("current_stage_id");
            entity.Property(e => e.Energy).HasColumnName("energy");
            entity.Property(e => e.LifeForce).HasColumnName("life_force");
            entity.Property(e => e.PetName)
                .HasMaxLength(50)
                .HasColumnName("pet_name");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.CurrentStage).WithMany(p => p.Pets)
                .HasForeignKey(d => d.CurrentStageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pets__current_st__1CBC4616");

            entity.HasOne(d => d.User).WithOne(p => p.Pet)
                .HasForeignKey<Pet>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pets__user_id__1BC821DD");
        });

        modelBuilder.Entity<PetEvolutionHistory>(entity =>
        {
            entity.HasKey(e => e.EvolutionId).HasName("PK__pet_evol__8A72FB1557D825B1");

            entity.ToTable("pet_evolution_history");

            entity.HasIndex(e => new { e.UserId, e.EvolvedAt }, "IX_pet_evolution_history_user_time").IsDescending(false, true);

            entity.Property(e => e.EvolutionId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("evolution_id");
            entity.Property(e => e.EvolvedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("evolved_at");
            entity.Property(e => e.FromStageId).HasColumnName("from_stage_id");
            entity.Property(e => e.ToStageId).HasColumnName("to_stage_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.FromStage).WithMany(p => p.PetEvolutionHistoryFromStages)
                .HasForeignKey(d => d.FromStageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pet_evolu__from___236943A5");

            entity.HasOne(d => d.ToStage).WithMany(p => p.PetEvolutionHistoryToStages)
                .HasForeignKey(d => d.ToStageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pet_evolu__to_st__245D67DE");

            entity.HasOne(d => d.User).WithMany(p => p.PetEvolutionHistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pet_evolu__user___22751F6C");
        });

        modelBuilder.Entity<PetLevel>(entity =>
        {
            entity.HasKey(e => e.LevelNo).HasName("PK__pet_leve__03463ED14924C336");

            entity.ToTable("pet_levels");

            entity.HasIndex(e => e.MinLifeForce, "UQ__pet_leve__80046810E4B91C6D").IsUnique();

            entity.Property(e => e.LevelNo).HasColumnName("level_no");
            entity.Property(e => e.MinLifeForce).HasColumnName("min_life_force");
        });

        modelBuilder.Entity<PetSpecy>(entity =>
        {
            entity.HasKey(e => e.SpeciesId).HasName("PK__pet_spec__B23DC5C2BD286DE6");

            entity.ToTable("pet_species");

            entity.HasIndex(e => e.SpeciesName, "UQ__pet_spec__E552C10367E8E632").IsUnique();

            entity.Property(e => e.SpeciesId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("species_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.SpeciesName)
                .HasMaxLength(50)
                .HasColumnName("species_name");
        });

        modelBuilder.Entity<PetStage>(entity =>
        {
            entity.HasKey(e => e.StageId).HasName("PK__pet_stag__CFC787603C8BD916");

            entity.ToTable("pet_stages");

            entity.HasIndex(e => new { e.SpeciesId, e.StageNo }, "UQ__pet_stag__DEC1C05D61E4545C").IsUnique();

            entity.Property(e => e.StageId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("stage_id");
            entity.Property(e => e.RequiredLevel).HasColumnName("required_level");
            entity.Property(e => e.SpeciesId).HasColumnName("species_id");
            entity.Property(e => e.StageName)
                .HasMaxLength(50)
                .HasColumnName("stage_name");
            entity.Property(e => e.StageNo).HasColumnName("stage_no");

            entity.HasOne(d => d.RequiredLevelNavigation).WithMany(p => p.PetStages)
                .HasForeignKey(d => d.RequiredLevel)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pet_stage__requi__114A936A");

            entity.HasOne(d => d.Species).WithMany(p => p.PetStages)
                .HasForeignKey(d => d.SpeciesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pet_stage__speci__10566F31");
        });

        modelBuilder.Entity<PvpMatch>(entity =>
        {
            entity.HasKey(e => e.MatchId).HasName("PK__pvp_matc__9D7FCBA3DF8DFB6A");

            entity.ToTable("pvp_matches");

            entity.HasIndex(e => new { e.StatusCode, e.MatchTypeCode, e.CreatedAt }, "IX_pvp_matches_status_type_created").IsDescending(false, false, true);

            entity.Property(e => e.MatchId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("match_id");
            entity.Property(e => e.CancelReason)
                .HasMaxLength(200)
                .HasColumnName("cancel_reason");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.EndedAt)
                .HasPrecision(0)
                .HasColumnName("ended_at");
            entity.Property(e => e.MatchTypeCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ranked")
                .HasColumnName("match_type_code");
            entity.Property(e => e.StartedAt)
                .HasPrecision(0)
                .HasColumnName("started_at");
            entity.Property(e => e.StatusCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("created")
                .HasColumnName("status_code");
            entity.Property(e => e.WinnerUserId).HasColumnName("winner_user_id");

            entity.HasOne(d => d.WinnerUser).WithMany(p => p.PvpMatches)
                .HasForeignKey(d => d.WinnerUserId)
                .HasConstraintName("FK__pvp_match__winne__10216507");
        });

        modelBuilder.Entity<PvpMatchPlayer>(entity =>
        {
            entity.HasKey(e => new { e.MatchId, e.UserId }).HasName("PK__pvp_matc__76E428D31A239015");

            entity.ToTable("pvp_match_players");

            entity.HasIndex(e => new { e.UserId, e.MatchId }, "IX_pvp_match_players_user_match");

            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.FinishTimeMs).HasColumnName("finish_time_ms");
            entity.Property(e => e.IsReady).HasColumnName("is_ready");
            entity.Property(e => e.JoinedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("joined_at");
            entity.Property(e => e.PetLevelAtMatch).HasColumnName("pet_level_at_match");
            entity.Property(e => e.ResultCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("result_code");
            entity.Property(e => e.Score).HasColumnName("score");
            entity.Property(e => e.StepsAtMatch).HasColumnName("steps_at_match");

            entity.HasOne(d => d.Match).WithMany(p => p.PvpMatchPlayers)
                .HasForeignKey(d => d.MatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pvp_match__match__1A9EF37A");

            entity.HasOne(d => d.User).WithMany(p => p.PvpMatchPlayers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pvp_match__user___1B9317B3");
        });

        modelBuilder.Entity<RewardPackage>(entity =>
        {
            entity.HasKey(e => e.RewardPackageId).HasName("PK__reward_p__B3C8ED8FB48D553F");

            entity.ToTable("reward_packages");

            entity.HasIndex(e => e.PackageName, "UQ__reward_p__671434CA2EC35560").IsUnique();

            entity.Property(e => e.RewardPackageId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("reward_package_id");
            entity.Property(e => e.PackageName)
                .HasMaxLength(100)
                .HasColumnName("package_name");
            entity.Property(e => e.WalletAmount).HasColumnName("wallet_amount");
        });

        modelBuilder.Entity<RewardPackageItem>(entity =>
        {
            entity.HasKey(e => new { e.RewardPackageId, e.ItemId }).HasName("PK__reward_p__76E8CD727E26401F");

            entity.ToTable("reward_package_items");

            entity.Property(e => e.RewardPackageId).HasColumnName("reward_package_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.Item).WithMany(p => p.RewardPackageItems)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__reward_pa__item___02FC7413");

            entity.HasOne(d => d.RewardPackage).WithMany(p => p.RewardPackageItems)
                .HasForeignKey(d => d.RewardPackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__reward_pa__rewar__02084FDA");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__roles__760965CC4EC295A5");

            entity.ToTable("roles");

            entity.HasIndex(e => e.RoleCode, "UQ__roles__BAE63075DC6E6473").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.RoleCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("role_code");
            entity.Property(e => e.RoleName)
                .HasMaxLength(100)
                .HasColumnName("role_name");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<ShopItem>(entity =>
        {
            entity.HasKey(e => e.ShopItemId).HasName("PK__shop_ite__542F23D258C113A5");

            entity.ToTable("shop_items");

            entity.HasIndex(e => new { e.IsActive, e.PriceAmount }, "IX_shop_items_active_price");

            entity.HasIndex(e => new { e.ItemId, e.ItemQuantity }, "UQ__shop_ite__6DC2725C988470A7").IsUnique();

            entity.Property(e => e.ShopItemId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("shop_item_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.ItemQuantity)
                .HasDefaultValue(1)
                .HasColumnName("item_quantity");
            entity.Property(e => e.PriceAmount).HasColumnName("price_amount");

            entity.HasOne(d => d.Item).WithMany(p => p.ShopItems)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__shop_item__item___5AB9788F");
        });

        modelBuilder.Entity<ShopPurchase>(entity =>
        {
            entity.HasKey(e => e.PurchaseId).HasName("PK__shop_pur__87071CB9F842F8BF");

            entity.ToTable("shop_purchases");

            entity.HasIndex(e => new { e.UserId, e.PurchasedAt }, "IX_shop_purchases_user_time").IsDescending(false, true);

            entity.Property(e => e.PurchaseId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("purchase_id");
            entity.Property(e => e.PurchasedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("purchased_at");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1)
                .HasColumnName("quantity");
            entity.Property(e => e.ShopItemId).HasColumnName("shop_item_id");
            entity.Property(e => e.UnitPriceAmount).HasColumnName("unit_price_amount");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.ShopItem).WithMany(p => p.ShopPurchases)
                .HasForeignKey(d => d.ShopItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__shop_purc__shop___634EBE90");

            entity.HasOne(d => d.User).WithMany(p => p.ShopPurchases)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__shop_purc__user___625A9A57");
        });

        modelBuilder.Entity<StepGoal>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.EffectiveFrom }).HasName("PK__step_goa__ABB93157C3DB63F4");

            entity.ToTable("step_goals");

            entity.HasIndex(e => new { e.UserId, e.EffectiveFrom }, "IX_step_goals_user_effective_from_desc").IsDescending(false, true);

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.TargetSteps).HasColumnName("target_steps");

            entity.HasOne(d => d.User).WithMany(p => p.StepGoals)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__step_goal__user___2CF2ADDF");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.SettingKey).HasName("PK__system_s__0DFAC4262309E8F4");

            entity.ToTable("system_settings");

            entity.Property(e => e.SettingKey)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("setting_key");
            entity.Property(e => e.SettingValue)
                .HasMaxLength(200)
                .HasColumnName("setting_value");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__users__B9BE370F9420CD73");

            entity.ToTable("users");

            entity.HasIndex(e => e.RoleId, "IX_users_role_id");

            entity.HasIndex(e => new { e.StatusCode, e.CreatedAt }, "IX_users_status_created").IsDescending(false, true);

            entity.HasIndex(e => e.NormalizedEmail, "UX_users_normalized_email_active")
                .IsUnique()
                .HasFilter("([deleted_at] IS NULL)");

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("user_id");
            entity.Property(e => e.AccessFailedCount).HasColumnName("access_failed_count");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt)
                .HasPrecision(0)
                .HasColumnName("deleted_at");
            entity.Property(e => e.Email)
                .HasMaxLength(320)
                .HasColumnName("email");
            entity.Property(e => e.EmailConfirmed).HasColumnName("email_confirmed");
            entity.Property(e => e.LastLoginAt)
                .HasPrecision(0)
                .HasColumnName("last_login_at");
            entity.Property(e => e.LastLogoutAt)
                .HasPrecision(0)
                .HasColumnName("last_logout_at");
            entity.Property(e => e.LockoutEndAt)
                .HasPrecision(0)
                .HasColumnName("lockout_end_at");
            entity.Property(e => e.NormalizedEmail)
                .HasMaxLength(320)
                .HasColumnName("normalized_email");
            entity.Property(e => e.PasswordChangedAt)
                .HasPrecision(0)
                .HasColumnName("password_changed_at");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.StatusCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("active")
                .HasColumnName("status_code");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__users__role_id__440B1D61");
        });

        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.AchievementId }).HasName("PK__user_ach__9A7AA5E79934EB92");

            entity.ToTable("user_achievements");

            entity.HasIndex(e => new { e.UserId, e.ClaimedAt }, "IX_user_achievements_user_claimed");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AchievementId).HasColumnName("achievement_id");
            entity.Property(e => e.ClaimedAt)
                .HasPrecision(0)
                .HasColumnName("claimed_at");
            entity.Property(e => e.ProgressValue).HasColumnName("progress_value");
            entity.Property(e => e.UnlockedAt)
                .HasPrecision(0)
                .HasColumnName("unlocked_at");

            entity.HasOne(d => d.Achievement).WithMany(p => p.UserAchievements)
                .HasForeignKey(d => d.AchievementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_achi__achie__4C6B5938");

            entity.HasOne(d => d.User).WithMany(p => p.UserAchievements)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_achi__user___4B7734FF");
        });

        modelBuilder.Entity<UserMission>(entity =>
        {
            entity.HasKey(e => e.UserMissionId).HasName("PK__user_mis__5F82187124BE2426");

            entity.ToTable("user_missions");

            entity.HasIndex(e => new { e.UserId, e.StatusCode, e.CycleDate }, "IX_user_missions_user_status_cycle").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.UserId, e.MissionId, e.CycleDate }, "UQ__user_mis__4F168EE9DC9463FA").IsUnique();

            entity.Property(e => e.UserMissionId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("user_mission_id");
            entity.Property(e => e.AssignedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("assigned_at");
            entity.Property(e => e.ClaimedAt)
                .HasPrecision(0)
                .HasColumnName("claimed_at");
            entity.Property(e => e.CycleDate).HasColumnName("cycle_date");
            entity.Property(e => e.MissionId).HasColumnName("mission_id");
            entity.Property(e => e.ProgressValue).HasColumnName("progress_value");
            entity.Property(e => e.StatusCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("active")
                .HasColumnName("status_code");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Mission).WithMany(p => p.UserMissions)
                .HasForeignKey(d => d.MissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_miss__missi__40058253");

            entity.HasOne(d => d.User).WithMany(p => p.UserMissions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_miss__user___3F115E1A");
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.NotificationId }).HasName("PK__user_not__57BBAF4D85258CC1");

            entity.ToTable("user_notifications");

            entity.HasIndex(e => new { e.UserId, e.DeletedAt, e.ReadAt }, "IX_user_notifications_user_unread");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.DeletedAt)
                .HasPrecision(0)
                .HasColumnName("deleted_at");
            entity.Property(e => e.ReadAt)
                .HasPrecision(0)
                .HasColumnName("read_at");

            entity.HasOne(d => d.Notification).WithMany(p => p.UserNotifications)
                .HasForeignKey(d => d.NotificationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_noti__notif__7B264821");

            entity.HasOne(d => d.User).WithMany(p => p.UserNotifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_noti__user___7A3223E8");
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__user_pro__B9BE370FB9EC7658");

            entity.ToTable("user_profiles");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(500)
                .HasColumnName("avatar_url");
            entity.Property(e => e.Bio)
                .HasMaxLength(280)
                .HasColumnName("bio");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Dob).HasColumnName("dob");
            entity.Property(e => e.Gender)
                .HasMaxLength(15)
                .HasColumnName("gender");
            entity.Property(e => e.HasSeenStory).HasColumnName("has_seen_story");
            entity.Property(e => e.LanguageCode)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("vi-VN")
                .HasColumnName("language_code");
            entity.Property(e => e.NotificationsEnabled)
                .HasDefaultValue(true)
                .HasColumnName("notifications_enabled");
            entity.Property(e => e.ShowActivityStats)
                .HasDefaultValue(true)
                .HasColumnName("show_activity_stats");
            entity.Property(e => e.ThemeCode)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("light")
                .HasColumnName("theme_code");
            entity.Property(e => e.TimeZoneId)
                .HasMaxLength(64)
                .HasDefaultValue("Asia/Ho_Chi_Minh")
                .HasColumnName("time_zone_id");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(30)
                .HasColumnName("username");

            entity.HasOne(d => d.User).WithOne(p => p.UserProfile)
                .HasForeignKey<UserProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_prof__user___619B8048");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__wallets__B9BE370F6C9AD880");

            entity.ToTable("wallets");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.Balance).HasColumnName("balance");

            entity.HasOne(d => d.User).WithOne(p => p.Wallet)
                .HasForeignKey<Wallet>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__wallets__user_id__6C190EBB");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
