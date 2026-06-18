namespace DAL.DTO;

public class PlayerChallengeStateResponse
{
    public int CancelLimit { get; set; }

    public int CancelUsed { get; set; }

    public int CancelRemaining { get; set; }

    public PlayerChallengeResponse? CurrentChallenge { get; set; }
}
