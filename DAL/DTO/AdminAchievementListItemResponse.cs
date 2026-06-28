namespace DAL.DTO;

public class AdminAchievementListItemResponse
{
    public Guid AchievementId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? IconUrl { get; set; }

    public string ConditionText { get; set; } = string.Empty;

    public string RewardText { get; set; } = string.Empty;

    public string StatusName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
