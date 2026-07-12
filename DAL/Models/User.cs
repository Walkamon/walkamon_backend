using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class User
{
    public Guid UserId { get; set; }

    public int RoleId { get; set; }

    public string Email { get; set; } = null!;

    public string NormalizedEmail { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public bool EmailConfirmed { get; set; }

    public string StatusCode { get; set; } = null!;

    public int AccessFailedCount { get; set; }

    public DateTime? LockoutEndAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime? PasswordChangedAt { get; set; }

    public DateTime? LastLogoutAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<DailyStep> DailySteps { get; set; } = new List<DailyStep>();

    public virtual ICollection<DailyLoginRewardClaim> DailyLoginRewardClaims { get; set; } = new List<DailyLoginRewardClaim>();

    public virtual ICollection<DeviceToken> DeviceTokens { get; set; } = new List<DeviceToken>();

    public virtual ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();

    public virtual ICollection<FriendRequest> FriendRequestReceiverUsers { get; set; } = new List<FriendRequest>();

    public virtual ICollection<FriendRequest> FriendRequestSenderUsers { get; set; } = new List<FriendRequest>();

    public virtual ICollection<Friendship> FriendshipUserHighs { get; set; } = new List<Friendship>();

    public virtual ICollection<Friendship> FriendshipUserLows { get; set; } = new List<Friendship>();

    public virtual ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();

    public virtual MatchmakingQueue? MatchmakingQueue { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<OtpRequest> OtpRequests { get; set; } = new List<OtpRequest>();

    public virtual UserPet? UserPet { get; set; }

    public virtual ICollection<PetEvolutionHistory> PetEvolutionHistories { get; set; } = new List<PetEvolutionHistory>();

    public virtual ICollection<PvpMatchPlayer> PvpMatchPlayers { get; set; } = new List<PvpMatchPlayer>();

    public virtual ICollection<PvpMatch> PvpMatches { get; set; } = new List<PvpMatch>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<ShopPurchase> ShopPurchases { get; set; } = new List<ShopPurchase>();

    public virtual ICollection<StepGoal> StepGoals { get; set; } = new List<StepGoal>();

    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();

    public virtual ICollection<UserFeedback> UserFeedbackHandledByUsers { get; set; } = new List<UserFeedback>();

    public virtual ICollection<UserFeedback> UserFeedbackUsers { get; set; } = new List<UserFeedback>();

    public virtual ICollection<UserMission> UserMissions { get; set; } = new List<UserMission>();

    public virtual ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();

    public virtual UserProfile? UserProfile { get; set; }

    public virtual Wallet? Wallet { get; set; }
    public virtual ICollection<StreakRewardClaim> StreakRewardClaims { get; set; }
    = new List<StreakRewardClaim>();
    public virtual ICollection<PetInteraction> PetInteractions { get; set; }
    = new List<PetInteraction>();
}
