namespace DAL.DTO;

public class AdminMissionListResponse
{
    public AdminMissionSummaryResponse Summary { get; set; } = new();

    public List<AdminMissionListItemResponse> Missions { get; set; } = [];
}

public class AdminMissionSummaryResponse
{
    public int TotalMissions { get; set; }

    public int ActiveMissions { get; set; }

    public int WeeklyMissions { get; set; }

    public int MonthlyMissions { get; set; }

    public int TotalWalletAmount { get; set; }
}

public class AdminMissionListItemResponse
{
    public Guid MissionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ConditionText { get; set; } = string.Empty;

    public string RewardText { get; set; } = string.Empty;

    public string StatusName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class AdminMissionDetailResponse
{
    public Guid MissionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string MissionTypeCode { get; set; } = string.Empty;

    public string StatusName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int WalletAmount { get; set; }

    public List<AdminMissionRewardItemResponse> RewardItems { get; set; } = [];

    public List<AdminMissionConditionResponse> CompletionConditions { get; set; } = [];

    public List<AdminMissionConditionResponse> AssignmentConditions { get; set; } = [];
}

public class AdminMissionRewardItemResponse
{
    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }
}

public class CreateAdminMissionRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MissionTypeCode { get; set; }

    public bool IsActive { get; set; } = true;

    public int WalletAmount { get; set; }

    public List<AdminMissionRewardItemRequest> RewardItems { get; set; } = [];

    public List<AdminMissionConditionRequest> CompletionConditions { get; set; } = [];

    public List<AdminMissionConditionRequest> AssignmentConditions { get; set; } = [];
}

public class UpdateAdminMissionRequest : CreateAdminMissionRequest
{
}

public class AdminMissionRewardItemRequest
{
    public Guid ItemId { get; set; }

    public int Quantity { get; set; }
}

public class AdminMissionConditionRequest
{
    public string ConditionCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public Guid? ReferenceMissionId { get; set; }
}

public class AdminMissionConditionResponse
{
    public Guid MissionConditionId { get; set; }

    public string ConditionGroup { get; set; } = string.Empty;

    public string ConditionCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public Guid? ReferenceMissionId { get; set; }
}

public class UpdateAdminMissionStatusRequest
{
    public bool IsActive { get; set; }
}
