using DAL.DTO;
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
        Task<StepGoal?> GetTodayGoalAsync(Guid userId, DateOnly today);

        Task<StepGoal?> GetCurrentGoalAsync(Guid userId, DateOnly date);

        Task<DailyStep?> GetDailyStepAsync(Guid userId, DateOnly date);
        Task<List<DailyStep>> GetCompletedGoalHistoryAsync(Guid userId);
        Task<List<StepGoalHistoryResponse>> GetStepGoalHistoryAsync(Guid userId);

    }
}
