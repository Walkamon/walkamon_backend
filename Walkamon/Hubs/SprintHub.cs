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
    private readonly ILogger<SprintHub> _logger;

    public SprintHub(
        WalkamonContext context,
        IPvpSprintService pvpSprintService,
        IPvpPresenceTracker presenceTracker,
        ILogger<SprintHub> logger)
    {
        _context = context;
        _pvpSprintService = pvpSprintService;
        _presenceTracker = presenceTracker;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            if (_presenceTracker.RegisterConnection(userId, Context.ConnectionId))
                await PublishPresenceChangedAsync(userId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var userId) &&
            _presenceTracker.UnregisterConnection(userId, Context.ConnectionId))
        {
            await PublishPresenceChangedAsync(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinMatch(Guid matchId)
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId) || !await _context.PvpMatchPlayers.AnyAsync(x => x.MatchId == matchId && x.UserId == userId)) throw new HubException("You are not a participant in this Sprint match.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"match:{matchId}");
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
            await Clients.Groups(friendIds.Select(x => $"user:{x}").ToList())
                .SendAsync("presence.changed", envelope);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish PvP presence change for UserId={UserId}.",
                userId);
        }
    }
}
