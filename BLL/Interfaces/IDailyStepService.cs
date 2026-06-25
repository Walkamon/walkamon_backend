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
    }
}
