namespace DAL.DTO;

public class CancelPlayerChallengeResponse
{
    public Guid UserMissionId { get; set; }

    public string StatusCode { get; set; } = string.Empty;

    public int CancelLimit { get; set; }

    public int CancelUsed { get; set; }

    public int CancelRemaining { get; set; }
}
