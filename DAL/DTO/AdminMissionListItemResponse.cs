namespace DAL.DTO;

public class AdminMissionListItemResponse
{
    public Guid MissionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ConditionText { get; set; } = string.Empty;

    public string RewardText { get; set; } = string.Empty;

    public string StatusName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
