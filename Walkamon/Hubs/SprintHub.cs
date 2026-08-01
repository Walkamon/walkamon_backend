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

    public SprintHub(WalkamonContext context, IPvpSprintService pvpSprintService)
    {
        _context = context;
        _pvpSprintService = pvpSprintService;
    }

    public override async Task OnConnectedAsync()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var userId)) await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        await base.OnConnectedAsync();
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
}
