using BLL.Interfaces;
using DAL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Walkamon.Hubs;

[Authorize(Roles = "User")]
public sealed class PresenceHub : Hub
{
    private readonly WalkamonContext _context;
    private readonly IPvpPresenceTracker _presenceTracker;
    private readonly IHubContext<SprintHub> _sprintHub;
    private readonly ILogger<PresenceHub> _logger;

    public PresenceHub(
        WalkamonContext context,
        IPvpPresenceTracker presenceTracker,
        IHubContext<SprintHub> sprintHub,
        ILogger<PresenceHub> logger)
    {
        _context = context;
        _presenceTracker = presenceTracker;
        _sprintHub = sprintHub;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            var becameOnline = _presenceTracker.RegisterConnection(
                userId,
                GetPresenceConnectionKey(Context.ConnectionId));

            _logger.LogInformation(
                "SignalR connected MethodName={MethodName} Hub={Hub} UserId={UserId} ConnectionId={ConnectionId} BecameOnline={BecameOnline}",
                nameof(OnConnectedAsync),
                nameof(PresenceHub),
                userId,
                Context.ConnectionId,
                becameOnline);

            if (becameOnline)
                await PublishPresenceChangedAsync(userId);
        }
        else
        {
            _logger.LogWarning(
                "SignalR connection has no valid NameIdentifier MethodName={MethodName} Hub={Hub} ConnectionId={ConnectionId}",
                nameof(OnConnectedAsync),
                nameof(PresenceHub),
                Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var userId))
        {
            var becameOffline = _presenceTracker.UnregisterConnection(
                userId,
                GetPresenceConnectionKey(Context.ConnectionId));

            _logger.LogInformation(
                exception,
                "SignalR disconnected MethodName={MethodName} Hub={Hub} UserId={UserId} ConnectionId={ConnectionId} BecameOffline={BecameOffline}",
                nameof(OnDisconnectedAsync),
                nameof(PresenceHub),
                userId,
                Context.ConnectionId,
                becameOffline);

            if (becameOffline)
                await PublishPresenceChangedAsync(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task PublishPresenceChangedAsync(Guid userId)
    {
        try
        {
            var friendIds = await _context.Friendships.AsNoTracking()
                .Where(x => x.UserLowId == userId || x.UserHighId == userId)
                .Select(x => x.UserLowId == userId ? x.UserHighId : x.UserLowId)
                .ToListAsync();
            if (friendIds.Count == 0)
                return;

            var isOnline = _presenceTracker.IsOnline(userId);
            var isBusy = isOnline && await _context.PvpPlayerActivities.AsNoTracking()
                .AnyAsync(x => x.UserId == userId);
            var envelope = new
            {
                eventId = Guid.NewGuid(),
                eventType = "presence.changed",
                aggregateId = userId,
                payload = new
                {
                    userId,
                    isOnline,
                    pvpAvailabilityCode = !isOnline ? "offline" : isBusy ? "busy" : "available",
                    serverTime = DateTime.UtcNow
                }
            };
            var groups = friendIds.Select(x => $"user:{x}").ToList();

            await Task.WhenAll(
                Clients.Groups(groups).SendAsync("presence.changed", envelope),
                _sprintHub.Clients.Groups(groups).SendAsync("presence.changed", envelope));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish presence change for UserId={UserId}.",
                userId);
        }
    }

    private static string GetPresenceConnectionKey(string connectionId) =>
        $"presence:{connectionId}";
}
