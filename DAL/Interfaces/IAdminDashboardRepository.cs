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
        Task<DashboardResponseDto> GetDashboardAsync();
    }
}
