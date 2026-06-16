namespace DAL.DTO;

public class AdminChallengeListResponse
{
    public AdminChallengeSummaryResponse Summary { get; set; } = new();

    public List<AdminChallengeListItemResponse> Challenges { get; set; } = [];
}

public class AdminChallengeSummaryResponse
{
    public int TotalChallenges { get; set; }

    public int OngoingChallenges { get; set; }

    public int TotalParticipants { get; set; }
}

public class AdminChallengeListItemResponse
{
    public Guid ChallengeId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string TargetText { get; set; } = string.Empty;

    public string TimeText { get; set; } = string.Empty;

    public int Participants { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class AdminChallengeDetailResponse
{
    public Guid ChallengeId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string MetricCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public int Participants { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool IsCancelable { get; set; }

    public int WalletAmount { get; set; }

    public List<AdminChallengeRewardItemResponse> RewardItems { get; set; } = [];
}

public class AdminChallengeRewardItemResponse
{
    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }
}

public class CreateAdminChallengeRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string MetricCode { get; set; } = string.Empty;

    public int TargetValue { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public bool IsCancelable { get; set; }

    public bool IsActive { get; set; } = true;

    public int WalletAmount { get; set; }

    public List<AdminChallengeRewardItemRequest> RewardItems { get; set; } = [];
}

public class UpdateAdminChallengeRequest : CreateAdminChallengeRequest
{
}

public class AdminChallengeRewardItemRequest
{
    public Guid ItemId { get; set; }

    public int Quantity { get; set; }
}

public class UpdateAdminChallengeStatusRequest
{
    public bool IsActive { get; set; }
}
