namespace DAL.DTO;

public class PlayerAchievementItemResponse
{
    public Guid AchievementId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? IconUrl { get; set; }

    public string MetricCode { get; set; } = string.Empty;

    public int ProgressValue { get; set; }

    public int TargetValue { get; set; }

    public int WalletAmount { get; set; }

    public List<PlayerAchievementRewardItemResponse> RewardItems { get; set; } = [];

    public bool IsUnlocked { get; set; }

    public bool CanClaim { get; set; }

    public DateTime? UnlockedAt { get; set; }

    public DateTime? ClaimedAt { get; set; }
}
