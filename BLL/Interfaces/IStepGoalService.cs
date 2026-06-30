using DAL.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IStepGoalService
    {
        Task SetGoalAsync(
    Guid currentUserId,
    SetStepGoalRequest request);

        Task<GoalProgressResponse> GetGoalProgressAsync(Guid currentUserId);
        Task<LongestStreakResponse> GetLongestStreakAsync(Guid currentUserId);
        Task<CurrentStreakResponse> GetCurrentStreakAsync(Guid currentUserId);
    }
}
