namespace DAL.DTO;

public class AdminMissionConditionResponse
{
    public Guid MissionConditionId { get; set; }

    public string ConditionGroup { get; set; } = string.Empty;

    public string ConditionCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public Guid? ReferenceMissionId { get; set; }
}
