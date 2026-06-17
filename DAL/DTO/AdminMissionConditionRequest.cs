namespace DAL.DTO;

public class AdminMissionConditionRequest
{
    public string ConditionCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public Guid? ReferenceMissionId { get; set; }
}
