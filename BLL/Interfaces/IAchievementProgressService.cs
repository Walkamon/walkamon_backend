using System;
using System.Threading.Tasks;

namespace BLL.Interfaces;

public interface IAchievementProgressService
{
    Task AddProgressAsync(Guid userId, string metricCode, int amount);
    Task SetProgressMaxAsync(Guid userId, string metricCode, int value);
}
