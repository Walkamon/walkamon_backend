using DAL.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IStreakRewardService
    {
        Task<ClaimRewardResponse> ClaimRewardAsync(Guid currentUserId,CurrentStreakResponse currentStreak);
    }
}
