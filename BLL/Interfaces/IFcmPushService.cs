using DAL.Models;

namespace BLL.Interfaces;

public interface IFcmPushService
{
    bool IsConfigured { get; }

    Task SendAsync(
        DeviceToken deviceToken,
        Notification notification,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, object?>? parameters = null);
}
