using BLL.Interfaces;
using BLL.Options;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using DAL.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using DalNotification = DAL.Models.Notification;
using FirebaseNotification = FirebaseAdmin.Messaging.Notification;

namespace BLL.Service;

public class FcmPushService : IFcmPushService
{
    private static readonly object AppLock = new();

    private readonly FirebaseOptions _options;
    private readonly ILogger<FcmPushService> _logger;

    public FcmPushService(
        IOptions<FirebaseOptions> options,
        ILogger<FcmPushService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ProjectId)
        && (!string.IsNullOrWhiteSpace(_options.ServiceAccountJsonBase64)
            || (!string.IsNullOrWhiteSpace(_options.ServiceAccountPath)
                && File.Exists(_options.ServiceAccountPath)));

    public async Task SendAsync(
        DeviceToken deviceToken,
        DalNotification notification,
        CancellationToken cancellationToken = default)
    {
        var app = GetOrCreateApp();
        if (app == null)
        {
            _logger.LogWarning(
                "Firebase push skipped because Firebase:ProjectId and credentials are not configured.");
            throw new InvalidOperationException(
                "Firebase push is not configured. Check Firebase credentials and ProjectId.");
        }

        var message = new Message
        {
            Token = deviceToken.FcmToken,
            Notification = new FirebaseNotification
            {
                Title = notification.Title,
                Body = notification.Body,
                ImageUrl = notification.ImageUrl
            },
            Android = string.IsNullOrWhiteSpace(_options.AndroidChannelId)
                ? null
                : new AndroidConfig
                {
                    Notification = new AndroidNotification
                    {
                        ChannelId = _options.AndroidChannelId
                    }
                },
            Data = new Dictionary<string, string>
            {
                ["notificationId"] = notification.NotificationId.ToString(),
                ["typeCode"] = notification.NotificationTypeCode
            }
        };

        await FirebaseMessaging.GetMessaging(app)
            .SendAsync(message, dryRun: false, cancellationToken);
    }

    private FirebaseApp? GetOrCreateApp()
    {
        if (string.IsNullOrWhiteSpace(_options.ProjectId))
        {
            return null;
        }

        lock (AppLock)
        {
            var app = FirebaseApp.GetInstance(_options.ProjectId);
            if (app != null)
            {
                return app;
            }

            var credential = CreateCredential();
            if (credential == null)
            {
                return null;
            }

            return FirebaseApp.Create(
                new AppOptions
                {
                    Credential = credential,
                    ProjectId = _options.ProjectId
                },
                _options.ProjectId);
        }
    }

    private GoogleCredential? CreateCredential()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_options.ServiceAccountJsonBase64))
            {
                var json = Encoding.UTF8.GetString(
                    Convert.FromBase64String(
                        _options.ServiceAccountJsonBase64.Trim()));

                return CredentialFactory
                    .FromJson<ServiceAccountCredential>(json)
                    .ToGoogleCredential();
            }

            if (!string.IsNullOrWhiteSpace(_options.ServiceAccountPath)
                && File.Exists(_options.ServiceAccountPath))
            {
                return CredentialFactory
                    .FromFile<ServiceAccountCredential>(_options.ServiceAccountPath)
                    .ToGoogleCredential();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Firebase credentials could not be loaded from configuration.");
        }

        return null;
    }
}
