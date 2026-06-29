using DAL.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IDailyStepService
    {
        Task UpdateStepAsync(
            Guid userId,
            UpdateDailyStepRequest request);

        Task<DailyStepStatisticDto> GetDailyStatisticAsync(
        Guid userId,
        DateOnly? date);

        Task<DailyStepStatisticDto> GetWeeklyStatisticAsync(
    Guid userId,
    DateOnly? date);

        Task<DailyStepStatisticDto> GetMonthlyStatisticAsync(
            Guid userId,
            DateOnly? date);

        Task<LeaderboardDto> GetLeaderboardAsync(
    Guid currentUserId,
    LeaderboardType type,
    DateOnly? date);
    }
    public enum LeaderboardType
    {
        Daily,
        Weekly,
        Monthly
    }
}
