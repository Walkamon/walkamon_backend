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
    }
}
