namespace DAL.DTO;

public class AdminAchievementConditionRequest
{
    public string ConditionCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public Guid? ReferenceAchievementId { get; set; }
}
