using DAL.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IAdminDashboardRepository
    {
        Task<int> GetTotalUsersAsync();

        Task<int> GetActiveUsersTodayAsync();

        Task<long> GetTotalStepsAsync();

        Task<double> GetUserGrowthPercentageAsync();

        Task<double> GetAverageStepsPerDayAsync();

        Task<int> GetWalkingUsersTodayAsync();

        Task<double> GetCompareWithYesterdayAsync();

        Task<List<PetInteractionStatisticDto>> GetPetInteractionStatisticsAsync();
    }
}
