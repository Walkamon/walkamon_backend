using System;
using System.Threading.Tasks;

namespace BLL.Interfaces;

public interface IMissionProgressService
{
    Task AddProgressAsync(Guid userId, string metricCode, int amount);

    Task SetProgressMaxAsync(Guid userId, string metricCode, int value);

    Task<bool> ArePrerequisitesMetAsync(Guid userId, Guid missionId);
}
