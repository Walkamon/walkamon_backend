using DAL.DTO;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IDailyStepRepository
    {
        Task<DailyStep?> GetByUserAndDateAsync(
            Guid userId,
            DateOnly date);

        Task<DailyStep?> GetByDateAsync(Guid userId, DateOnly date);

        Task<List<DailyStep>> GetByDateRangeAsync(
            Guid userId,
            DateOnly fromDate,
            DateOnly toDate);

        Task<List<LeaderboardRawDto>> GetLeaderboardAsync(
    DateOnly fromDate,
    DateOnly toDate);
    }
}
