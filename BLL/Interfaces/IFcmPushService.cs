using DAL.Models;

namespace BLL.Interfaces;

public interface IFcmPushService
{
    bool IsConfigured { get; }

    // Keep the original three-argument contract so older workers/tests that
    // implement this interface remain source compatible.  The parameterized
    // overload below is additive and is used by localized notification sends.
    Task SendAsync(
        DeviceToken deviceToken,
        Notification notification,
        CancellationToken cancellationToken = default);

    async Task SendAsync(
        DeviceToken deviceToken,
        Notification notification,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        await SendAsync(deviceToken, notification, cancellationToken);
    }

    async Task SendLocalizedAsync(
        DeviceToken deviceToken,
        Notification notification,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, object?>? parameters = null,
        string? titleOverride = null,
        string? bodyOverride = null)
    {
        await SendAsync(deviceToken, notification, cancellationToken, parameters);
    }
}
