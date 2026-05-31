using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DAL.Models;

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

    public virtual DbSet<MatchmakingQueue> MatchmakingQueues { get; set; }

    public virtual DbSet<Misson> Missons { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OtpRequest> OtpRequests { get; set; }

    public virtual DbSet<Pet> Pets { get; set; }

    public virtual DbSet<PetEvolutionHistory> PetEvolutionHistories { get; set; }

    public virtual DbSet<PetLevel> PetLevels { get; set; }

    public virtual DbSet<PetSpecy> PetSpecies { get; set; }

    public virtual DbSet<PetStage> PetStages { get; set; }

    public virtual DbSet<PvpMatch> PvpMatches { get; set; }

    public virtual DbSet<PvpMatchPlayer> PvpMatchPlayers { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<RewardPackage> RewardPackages { get; set; }

    public virtual DbSet<RewardPackageItem> RewardPackageItems { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Shop> Shops { get; set; }

    public virtual DbSet<ShopItem> ShopItems { get; set; }

    public virtual DbSet<ShopPurchase> ShopPurchases { get; set; }

    public virtual DbSet<StepGoal> StepGoals { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAchievement> UserAchievements { get; set; }

    public virtual DbSet<UserMisson> UserMissons { get; set; }

    public virtual DbSet<UserNotification> UserNotifications { get; set; }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    public virtual DbSet<Wallet> Wallets { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=MYDEVICE\\HUNGG;Database=Walkamon;User Id=sa;Password=123456;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.HasKey(e => e.AchievementId).HasName("PK__achievem__3C492E83FF73D83A");

            entity.ToTable("achievements");

            entity.HasIndex(e => e.Title, "UQ__achievem__E52A1BB3B2153FC2").IsUnique();

            entity.Property(e => e.AchievementId).HasColumnName("achievement_id");
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
                .HasConstraintName("FK__achieveme__rewar__3E1D39E1");
        });

        modelBuilder.Entity<DailyStep>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StepDate }).HasName("PK__daily_st__37CD0C070A2450CC");

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
                .HasConstraintName("FK__daily_ste__user___236943A5");
        });

        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.HasKey(e => e.DeviceTokenId).HasName("PK__device_t__3ADABB7D5BEE35C4");

            entity.ToTable("device_tokens");

            entity.HasIndex(e => new { e.UserId, e.IsActive }, "IX_device_tokens_user_active");

            entity.HasIndex(e => e.FcmToken, "UQ__device_t__61C3D35DDD477B22").IsUnique();

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
                .HasConstraintName("FK__device_to__user___793DFFAF");
        });

        modelBuilder.Entity<ExternalLogin>(entity =>
        {
            entity.HasKey(e => e.ExternalLoginId).HasName("PK__external__71E083FB2BF0E4B4");

            entity.ToTable("external_logins");

            entity.HasIndex(e => e.UserId, "IX_external_logins_user_id");

            entity.HasIndex(e => new { e.ProviderName, e.ProviderSubject }, "UQ__external__8B7190E1D1747E23").IsUnique();

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
            entity.HasKey(e => e.RequestId).HasName("PK__friend_r__18D3B90FBED9206F");

            entity.ToTable("friend_requests");

            entity.HasIndex(e => new { e.ReceiverUserId, e.StatusCode, e.CreatedAt }, "IX_friend_requests_receiver_status_created").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.SenderUserId, e.ReceiverUserId }, "UX_friend_requests_pending_sender_receiver")
                .IsUnique()
                .HasFilter("([status_code]='pending')");

            entity.Property(e => e.RequestId).HasColumnName("request_id");
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
                .HasConstraintName("FK__friend_re__recei__662B2B3B");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.FriendRequestSenderUsers)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__friend_re__sende__65370702");
        });

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => new { e.UserLowId, e.UserHighId }).HasName("PK__friendsh__4846FE8B1A2C617E");

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
                .HasConstraintName("FK__friendshi__user___6BE40491");

            entity.HasOne(d => d.UserLow).WithMany(p => p.FriendshipUserLows)
                .HasForeignKey(d => d.UserLowId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__friendshi__user___6AEFE058");
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ItemId }).HasName("PK__inventor__7C9E17F2D3B44D69");

            entity.ToTable("inventory_items");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.Item).WithMany(p => p.InventoryItems)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__inventory__item___498EEC8D");

            entity.HasOne(d => d.User).WithMany(p => p.InventoryItems)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__inventory__user___489AC854");
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.ItemId).HasName("PK__items__52020FDDB08D3543");

            entity.ToTable("items");

            entity.HasIndex(e => e.ItemName, "UQ__items__ACA52A9731D5E75C").IsUnique();

            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Description)
                .HasMaxLength(300)
                .HasColumnName("description");
            entity.Property(e => e.EffectTypeCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("effect_type_code");
            entity.Property(e => e.EffectValue).HasColumnName("effect_value");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ItemName)
                .HasMaxLength(80)
                .HasColumnName("item_name");
            entity.Property(e => e.ItemTypeCode)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("item_type_code");
        });

        modelBuilder.Entity<MatchmakingQueue>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__matchmak__B9BE370FEBF379F4");

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
                .HasConstraintName("FK__matchmaki__user___04AFB25B");
        });

        modelBuilder.Entity<Misson>(entity =>
        {
            entity.HasKey(e => e.MissonId).HasName("PK__missons__5CB5835753F48AEE");

            entity.ToTable("missons");

            entity.HasIndex(e => new { e.MissonTypeCode, e.IsActive, e.StartAt, e.EndAt }, "IX_missons_type_active_window");

            entity.Property(e => e.MissonId).HasColumnName("misson_id");
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
            entity.Property(e => e.MissonTypeCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("misson_type_code");
            entity.Property(e => e.RewardPackageId).HasColumnName("reward_package_id");
            entity.Property(e => e.StartAt)
                .HasPrecision(0)
                .HasColumnName("start_at");
            entity.Property(e => e.TargetValue).HasColumnName("target_value");
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .HasColumnName("title");

            entity.HasOne(d => d.RewardPackage).WithMany(p => p.Missons)
                .HasForeignKey(d => d.RewardPackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__missons__reward___2EDAF651");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__notifica__E059842FD74753AD");

            entity.ToTable("notifications");

            entity.HasIndex(e => new { e.ScheduledAt, e.CreatedAt }, "IX_notifications_schedule").IsDescending(false, true);

            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
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

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("FK__notificat__creat__6FB49575");
        });

        modelBuilder.Entity<OtpRequest>(entity =>
        {
            entity.HasKey(e => e.OtpRequestId).HasName("PK__otp_requ__638D23F5C305A757");

            entity.ToTable("otp_requests");

            entity.HasIndex(e => new { e.UserId, e.PurposeCode, e.StatusCode, e.ExpiresAt }, "IX_otp_requests_user_purpose_status");

            entity.HasIndex(e => e.RequestCode, "UQ_otp_requests_request_code").IsUnique();

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

            entity.HasOne(d => d.User).WithMany(p => p.OtpRequests)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__otp_reque__user___5DCAEF64");
        });

        modelBuilder.Entity<Pet>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__pets__B9BE370F74A67B4B");

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
                .HasConstraintName("FK__pets__current_st__17F790F9");

            entity.HasOne(d => d.User).WithOne(p => p.Pet)
                .HasForeignKey<Pet>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pets__user_id__17036CC0");
        });

        modelBuilder.Entity<PetEvolutionHistory>(entity =>
        {
            entity.HasKey(e => e.EvolutionId).HasName("PK__pet_evol__8A72FB151D99940C");

            entity.ToTable("pet_evolution_history");

            entity.HasIndex(e => new { e.UserId, e.EvolvedAt }, "IX_pet_evolution_history_user_time").IsDescending(false, true);

            entity.Property(e => e.EvolutionId).HasColumnName("evolution_id");
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
                .HasConstraintName("FK__pet_evolu__from___1DB06A4F");

            entity.HasOne(d => d.ToStage).WithMany(p => p.PetEvolutionHistoryToStages)
                .HasForeignKey(d => d.ToStageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pet_evolu__to_st__1EA48E88");

            entity.HasOne(d => d.User).WithMany(p => p.PetEvolutionHistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pet_evolu__user___1CBC4616");
        });

        modelBuilder.Entity<PetLevel>(entity =>
        {
            entity.HasKey(e => e.LevelNo).HasName("PK__pet_leve__03463ED1031B117E");

            entity.ToTable("pet_levels");

            entity.HasIndex(e => e.MinLifeForce, "UQ__pet_leve__8004681028597B3F").IsUnique();

            entity.Property(e => e.LevelNo).HasColumnName("level_no");
            entity.Property(e => e.MinLifeForce).HasColumnName("min_life_force");
        });

        modelBuilder.Entity<PetSpecy>(entity =>
        {
            entity.HasKey(e => e.SpeciesId).HasName("PK__pet_spec__B23DC5C22F9A93D7");

            entity.ToTable("pet_species");

            entity.HasIndex(e => e.SpeciesName, "UQ__pet_spec__E552C10341F16A2D").IsUnique();

            entity.Property(e => e.SpeciesId).HasColumnName("species_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.SpeciesName)
                .HasMaxLength(50)
                .HasColumnName("species_name");
        });

        modelBuilder.Entity<PetStage>(entity =>
        {
            entity.HasKey(e => e.StageId).HasName("PK__pet_stag__CFC787600A2AC94C");

            entity.ToTable("pet_stages");

            entity.HasIndex(e => new { e.SpeciesId, e.StageNo }, "UQ__pet_stag__DEC1C05D1242869C").IsUnique();

            entity.Property(e => e.StageId).HasColumnName("stage_id");
            entity.Property(e => e.RequiredLevel).HasColumnName("required_level");
            entity.Property(e => e.SpeciesId).HasColumnName("species_id");
            entity.Property(e => e.StageName)
                .HasMaxLength(50)
                .HasColumnName("stage_name");
            entity.Property(e => e.StageNo).HasColumnName("stage_no");

            entity.HasOne(d => d.RequiredLevelNavigation).WithMany(p => p.PetStages)
                .HasForeignKey(d => d.RequiredLevel)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pet_stage__requi__0C85DE4D");

            entity.HasOne(d => d.Species).WithMany(p => p.PetStages)
                .HasForeignKey(d => d.SpeciesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pet_stage__speci__0B91BA14");
        });

        modelBuilder.Entity<PvpMatch>(entity =>
        {
            entity.HasKey(e => e.MatchId).HasName("PK__pvp_matc__9D7FCBA392BC41BB");

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
                .HasConstraintName("FK__pvp_match__winne__0E391C95");
        });

        modelBuilder.Entity<PvpMatchPlayer>(entity =>
        {
            entity.HasKey(e => new { e.MatchId, e.UserId }).HasName("PK__pvp_matc__76E428D392A589FE");

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
                .HasConstraintName("FK__pvp_match__match__18B6AB08");

            entity.HasOne(d => d.User).WithMany(p => p.PvpMatchPlayers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__pvp_match__user___19AACF41");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.RefreshTokenId).HasName("PK__refresh___B0A1F7C77D23AEC5");

            entity.ToTable("refresh_tokens");

            entity.HasIndex(e => new { e.UserId, e.RevokedAt, e.ExpiresAt }, "IX_refresh_tokens_user_active");

            entity.HasIndex(e => e.TokenHash, "UQ__refresh___9F6BDB13DBB94F11").IsUnique();

            entity.Property(e => e.RefreshTokenId).HasColumnName("refresh_token_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.DeviceId)
                .HasMaxLength(100)
                .HasColumnName("device_id");
            entity.Property(e => e.DeviceName)
                .HasMaxLength(200)
                .HasColumnName("device_name");
            entity.Property(e => e.ExpiresAt)
                .HasPrecision(0)
                .HasColumnName("expires_at");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false)
                .HasColumnName("ip_address");
            entity.Property(e => e.JwtId).HasColumnName("jwt_id");
            entity.Property(e => e.LastUsedAt)
                .HasPrecision(0)
                .HasColumnName("last_used_at");
            entity.Property(e => e.ReplacedByTokenId).HasColumnName("replaced_by_token_id");
            entity.Property(e => e.RevokedAt)
                .HasPrecision(0)
                .HasColumnName("revoked_at");
            entity.Property(e => e.RevokedReason)
                .HasMaxLength(200)
                .HasColumnName("revoked_reason");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(32)
                .IsFixedLength()
                .HasColumnName("token_hash");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(500)
                .HasColumnName("user_agent");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.ReplacedByToken).WithMany(p => p.InverseReplacedByToken)
                .HasForeignKey(d => d.ReplacedByTokenId)
                .HasConstraintName("FK__refresh_t__repla__5070F446");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__refresh_t__user___4F7CD00D");
        });

        modelBuilder.Entity<RewardPackage>(entity =>
        {
            entity.HasKey(e => e.RewardPackageId).HasName("PK__reward_p__B3C8ED8F0DB49BEA");

            entity.ToTable("reward_packages");

            entity.HasIndex(e => e.PackageName, "UQ__reward_p__671434CA607B3572").IsUnique();

            entity.Property(e => e.RewardPackageId).HasColumnName("reward_package_id");
            entity.Property(e => e.PackageName)
                .HasMaxLength(100)
                .HasColumnName("package_name");
            entity.Property(e => e.WalletAmount).HasColumnName("wallet_amount");
        });

        modelBuilder.Entity<RewardPackageItem>(entity =>
        {
            entity.HasKey(e => new { e.RewardPackageId, e.ItemId }).HasName("PK__reward_p__76E8CD72BDCC22A0");

            entity.ToTable("reward_package_items");

            entity.Property(e => e.RewardPackageId).HasColumnName("reward_package_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.Item).WithMany(p => p.RewardPackageItems)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__reward_pa__item___00200768");

            entity.HasOne(d => d.RewardPackage).WithMany(p => p.RewardPackageItems)
                .HasForeignKey(d => d.RewardPackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__reward_pa__rewar__7F2BE32F");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__roles__760965CCCAE21261");

            entity.ToTable("roles");

            entity.HasIndex(e => e.RoleCode, "UQ__roles__BAE63075DF416FF2").IsUnique();

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

        modelBuilder.Entity<Shop>(entity =>
        {
            entity.HasKey(e => e.ShopId).HasName("PK__shops__AD0817868312F063");

            entity.ToTable("shops");

            entity.HasIndex(e => e.ShopName, "UQ__shops__E5A7FE103DB5D1E7").IsUnique();

            entity.Property(e => e.ShopId).HasColumnName("shop_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ShopName)
                .HasMaxLength(50)
                .HasColumnName("shop_name");
        });

        modelBuilder.Entity<ShopItem>(entity =>
        {
            entity.HasKey(e => e.ShopItemId).HasName("PK__shop_ite__542F23D223F5BCA4");

            entity.ToTable("shop_items");

            entity.HasIndex(e => new { e.ShopId, e.IsActive, e.PriceAmount }, "IX_shop_items_shop_active_price");

            entity.HasIndex(e => new { e.ShopId, e.ItemId, e.ItemQuantity }, "UQ__shop_ite__6BD430A202984ADB").IsUnique();

            entity.Property(e => e.ShopItemId).HasColumnName("shop_item_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.ItemQuantity)
                .HasDefaultValue(1)
                .HasColumnName("item_quantity");
            entity.Property(e => e.PriceAmount).HasColumnName("price_amount");
            entity.Property(e => e.ShopId).HasColumnName("shop_id");

            entity.HasOne(d => d.Item).WithMany(p => p.ShopItems)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__shop_item__item___55F4C372");

            entity.HasOne(d => d.Shop).WithMany(p => p.ShopItems)
                .HasForeignKey(d => d.ShopId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__shop_item__shop___55009F39");
        });

        modelBuilder.Entity<ShopPurchase>(entity =>
        {
            entity.HasKey(e => e.PurchaseId).HasName("PK__shop_pur__87071CB9F53E54AB");

            entity.ToTable("shop_purchases");

            entity.HasIndex(e => new { e.UserId, e.PurchasedAt }, "IX_shop_purchases_user_time").IsDescending(false, true);

            entity.Property(e => e.PurchaseId).HasColumnName("purchase_id");
            entity.Property(e => e.ItemQuantitySnapshot).HasColumnName("item_quantity_snapshot");
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
                .HasConstraintName("FK__shop_purc__shop___5E8A0973");

            entity.HasOne(d => d.User).WithMany(p => p.ShopPurchases)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__shop_purc__user___5D95E53A");
        });

        modelBuilder.Entity<StepGoal>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.EffectiveFrom }).HasName("PK__step_goa__ABB93157CD105D91");

            entity.ToTable("step_goals");

            entity.HasIndex(e => new { e.UserId, e.EffectiveFrom }, "IX_step_goals_user_effective_from_desc").IsDescending(false, true);

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.TargetSteps).HasColumnName("target_steps");

            entity.HasOne(d => d.User).WithMany(p => p.StepGoals)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__step_goal__user___2739D489");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.SettingKey).HasName("PK__system_s__0DFAC42640E6A53C");

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
            entity.HasKey(e => e.UserId).HasName("PK__users__B9BE370F64AEF214");

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
            entity.HasKey(e => new { e.UserId, e.AchievementId }).HasName("PK__user_ach__9A7AA5E70CF4D908");

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
                .HasConstraintName("FK__user_achi__achie__43D61337");

            entity.HasOne(d => d.User).WithMany(p => p.UserAchievements)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_achi__user___42E1EEFE");
        });

        modelBuilder.Entity<UserMisson>(entity =>
        {
            entity.HasKey(e => e.UserMissonId).HasName("PK__user_mis__5CAFECA0C3499EDC");

            entity.ToTable("user_missons");

            entity.HasIndex(e => new { e.UserId, e.StatusCode, e.CycleDate }, "IX_user_missons_user_status_cycle").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.UserId, e.MissonId, e.CycleDate }, "UQ__user_mis__1189CF770E27797E").IsUnique();

            entity.Property(e => e.UserMissonId).HasColumnName("user_misson_id");
            entity.Property(e => e.AssignedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("assigned_at");
            entity.Property(e => e.ClaimedAt)
                .HasPrecision(0)
                .HasColumnName("claimed_at");
            entity.Property(e => e.CycleDate).HasColumnName("cycle_date");
            entity.Property(e => e.MissonId).HasColumnName("misson_id");
            entity.Property(e => e.ProgressValue).HasColumnName("progress_value");
            entity.Property(e => e.StatusCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("active")
                .HasColumnName("status_code");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Misson).WithMany(p => p.UserMissons)
                .HasForeignKey(d => d.MissonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_miss__misso__3864608B");

            entity.HasOne(d => d.User).WithMany(p => p.UserMissons)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_miss__user___37703C52");
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.NotificationId }).HasName("PK__user_not__57BBAF4DABE24A21");

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
                .HasConstraintName("FK__user_noti__notif__73852659");

            entity.HasOne(d => d.User).WithMany(p => p.UserNotifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__user_noti__user___72910220");
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__user_pro__B9BE370F669FE4D8");

            entity.ToTable("user_profiles");

            entity.HasIndex(e => e.NormalizedUsername, "UX_user_profiles_normalized_username")
                .IsUnique()
                .HasFilter("([normalized_username] IS NOT NULL)");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.AllowFriendRequests)
                .HasDefaultValue(true)
                .HasColumnName("allow_friend_requests");
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
            entity.Property(e => e.DisplayName)
                .HasMaxLength(80)
                .HasColumnName("display_name");
            entity.Property(e => e.LanguageCode)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("vi-VN")
                .HasColumnName("language_code");
            entity.Property(e => e.NormalizedUsername)
                .HasMaxLength(30)
                .HasColumnName("normalized_username");
            entity.Property(e => e.NotificationsEnabled)
                .HasDefaultValue(true)
                .HasColumnName("notifications_enabled");
            entity.Property(e => e.ProfileVisibilityCode)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("public")
                .HasColumnName("profile_visibility_code");
            entity.Property(e => e.QuietHourEnd).HasColumnName("quiet_hour_end");
            entity.Property(e => e.QuietHourStart).HasColumnName("quiet_hour_start");
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
                .HasConstraintName("FK__user_prof__user___6D0D32F4");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__wallets__B9BE370F7C81595B");

            entity.ToTable("wallets");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.Balance).HasColumnName("balance");

            entity.HasOne(d => d.User).WithOne(p => p.Wallet)
                .HasForeignKey<Wallet>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__wallets__user_id__71D1E811");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
