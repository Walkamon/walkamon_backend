using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Service
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly IAdminDashboardRepository _repository;

        public AdminDashboardService(IAdminDashboardRepository repository)
        {
            _repository = repository;
        }

        public async Task<DashboardResponseDto> GetDashboardAsync()
        {
            var totalUsersTask = _repository.GetTotalUsersAsync();

            var activeUsersTask = _repository.GetActiveUsersTodayAsync();

            var totalStepsTask = _repository.GetTotalStepsAsync();

            var growthTask = _repository.GetUserGrowthPercentageAsync();

            var averageTask = _repository.GetAverageStepsPerDayAsync();

            var walkingTodayTask = _repository.GetWalkingUsersTodayAsync();

            var compareTask = _repository.GetCompareWithYesterdayAsync();

            var petTask = _repository.GetPetInteractionStatisticsAsync();

            await Task.WhenAll(
                totalUsersTask,
                activeUsersTask,
                totalStepsTask,
                growthTask,
                averageTask,
                walkingTodayTask,
                compareTask,
                petTask);

            return new DashboardResponseDto
            {
                TotalUsers = totalUsersTask.Result,

                ActiveUsers = activeUsersTask.Result,

                TotalSteps = totalStepsTask.Result,

                UserGrowthPercentage = growthTask.Result,

                AverageStepsPerDay = averageTask.Result,

                WalkingUsersToday = walkingTodayTask.Result,

                CompareWithYesterday = compareTask.Result,

                PetInteractions = petTask.Result
            };
        }
    }
}
