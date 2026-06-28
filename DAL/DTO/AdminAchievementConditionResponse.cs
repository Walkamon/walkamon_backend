namespace DAL.DTO;

public class AdminAchievementConditionResponse
{
    public Guid AchievementConditionId { get; set; }

    public string ConditionGroup { get; set; } = string.Empty;

    public string ConditionCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public Guid? ReferenceAchievementId { get; set; }
}
