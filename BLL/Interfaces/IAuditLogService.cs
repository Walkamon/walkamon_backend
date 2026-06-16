using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IAuditLogService
    {
        Task<IEnumerable<AuditLog>> GetAllAsync();

        Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId);
    }
}
