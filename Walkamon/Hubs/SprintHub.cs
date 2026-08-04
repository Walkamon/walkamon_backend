using BLL.Interfaces;
using BLL.Exceptions;
using DAL.Data;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Walkamon.Hubs;

[Authorize(Roles = "User")]
public sealed class SprintHub : Hub
{
    private readonly WalkamonContext _context;
    private readonly IPvpSprintService _pvpSprintService;
    private readonly IPvpPresenceTracker _presenceTracker;
    private readonly IHubContext<PresenceHub> _presenceHub;
    private readonly ILogger<SprintHub> _logger;

    public SprintHub(
        WalkamonContext context,
        IPvpSprintService pvpSprintService,
        IPvpPresenceTracker presenceTracker,
        IHubContext<PresenceHub> presenceHub,
        ILogger<SprintHub> logger)
    {
        _context = context;
        _pvpSprintService = pvpSprintService;
        _presenceTracker = presenceTracker;
        _presenceHub = presenceHub;
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
                GetSprintConnectionKey(Context.ConnectionId));
            _logger.LogInformation(
                "SignalR connected MethodName={MethodName} Hub={Hub} UserId={UserId} ConnectionId={ConnectionId} BecameOnline={BecameOnline}",
                nameof(OnConnectedAsync),
                nameof(SprintHub),
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
                nameof(SprintHub),
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
                GetSprintConnectionKey(Context.ConnectionId));
            _logger.LogInformation(
                exception,
                "SignalR disconnected MethodName={MethodName} Hub={Hub} UserId={UserId} ConnectionId={ConnectionId} BecameOffline={BecameOffline}",
                nameof(OnDisconnectedAsync),
                nameof(SprintHub),
                userId,
                Context.ConnectionId,
                becameOffline);
            if (becameOffline)
                await PublishPresenceChangedAsync(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinMatch(Guid matchId)
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new HubException("The authenticated user is invalid.");
        if (!await _context.PvpMatchPlayers.AsNoTracking()
                .AnyAsync(x => x.MatchId == matchId && x.UserId == userId))
            throw new HubException("You are not a participant in this Sprint match.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"match:{matchId}");
        await _context.PvpMatchPlayers
            .Where(x => x.MatchId == matchId && x.UserId == userId && x.RealtimeJoinedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RealtimeJoinedAt, DateTime.UtcNow));
    }

    public async Task<PvpMatchReadyResponse> ReadyMatch(Guid matchId)
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId)) throw new HubException("The authenticated user is invalid.");

        try
        {
            return await _pvpSprintService.ReadyMatchAsync(userId, matchId);
        }
        catch (AppException exception)
        {
            throw new HubException(exception.Message);
        }
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
            var payload = new
            {
                userId,
                isOnline,
                pvpAvailabilityCode = !isOnline ? "offline" : isBusy ? "busy" : "available",
                serverTime = DateTime.UtcNow
            };
            var envelope = new
            {
                eventId = Guid.NewGuid(),
                eventType = "presence.changed",
                aggregateId = userId,
                payload
            };
            var groups = friendIds.Select(x => $"user:{x}").ToList();
            await Task.WhenAll(
                Clients.Groups(groups).SendAsync("presence.changed", envelope),
                _presenceHub.Clients.Groups(groups).SendAsync("presence.changed", envelope));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish PvP presence change for UserId={UserId}.",
                userId);
        }
    }

    private static string GetSprintConnectionKey(string connectionId) =>
        $"sprint:{connectionId}";
}
