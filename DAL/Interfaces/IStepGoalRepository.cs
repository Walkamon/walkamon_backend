using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IStepGoalRepository
    {
        Task<Models.StepGoal?> GetTodayGoalAsync(Guid userId, DateOnly today);

        Task<Models.StepGoal?> GetCurrentGoalAsync(Guid userId, DateOnly date);

        Task<DailyStep?> GetDailyStepAsync(Guid userId, DateOnly date);
        Task<List<DailyStep>> GetCompletedGoalHistoryAsync(Guid userId);

    }
}
