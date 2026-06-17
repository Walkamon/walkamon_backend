namespace DAL.DTO;

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
