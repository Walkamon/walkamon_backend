namespace DAL.DTO;

public class AdminAchievementDetailResponse
{
    public Guid AchievementId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? IconUrl { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int WalletAmount { get; set; }

    public string MetricCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public List<AdminAchievementRewardItemResponse> RewardItems { get; set; } = [];

    public List<AdminAchievementConditionResponse> AssignmentConditions { get; set; } = [];
}
