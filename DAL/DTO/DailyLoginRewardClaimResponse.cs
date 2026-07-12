namespace DAL.DTO;

public class DailyLoginRewardClaimResponse
{
    public DateOnly ClaimDate { get; set; }

    public int ClaimedDay { get; set; }

    public int Reward { get; set; }

    public int Balance { get; set; }

    public int NextDay { get; set; }
}
