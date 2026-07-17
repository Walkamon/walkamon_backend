using DAL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Walkamon.Hubs;

[Authorize(Roles = "User")]
public sealed class SprintHub : Hub
{
    private readonly WalkamonContext _context;
    public SprintHub(WalkamonContext context) => _context = context;

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
}
