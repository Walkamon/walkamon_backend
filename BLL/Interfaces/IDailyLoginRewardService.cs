using DAL.DTO;

namespace BLL.Interfaces;

public interface IDailyLoginRewardService
{
    Task<DailyLoginRewardCalendarResponse> GetCalendarAsync(Guid userId);

    Task<DailyLoginRewardClaimResponse> ClaimAsync(Guid userId);
}
