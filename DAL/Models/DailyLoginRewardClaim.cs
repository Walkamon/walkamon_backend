namespace DAL.Models;

public partial class DailyLoginRewardClaim
{
    public Guid UserId { get; set; }

    public DateOnly ClaimDate { get; set; }

    public int CycleDay { get; set; }

    public int Reward { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
