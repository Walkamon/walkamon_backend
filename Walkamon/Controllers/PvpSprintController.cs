using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/pvp/sprint")]
public sealed class PvpSprintController : BaseController
{
    private readonly IPvpSprintService _service;
    public PvpSprintController(IPvpSprintService service) => _service = service;

    [HttpPost("invites")]
    public async Task<IActionResult> CreateInvite(CreatePvpSprintInviteRequest request) => Ok(Success(await _service.CreateInviteAsync(CurrentUserId, request), "Sprint invite created."));

    [HttpPost("invites/{inviteId:guid}/response")]
    public async Task<IActionResult> RespondInvite(Guid inviteId, RespondPvpSprintInviteRequest request) => Ok(Success(await _service.RespondInviteAsync(CurrentUserId, inviteId, request), "Sprint invite updated."));

    [HttpDelete("invites/{inviteId:guid}")]
    public async Task<IActionResult> CancelInvite(Guid inviteId) { await _service.CancelInviteAsync(CurrentUserId, inviteId); return Ok(Success<object?>(null, "Sprint invite cancelled.")); }

    [HttpGet("invites")]
    public async Task<IActionResult> GetInvites([FromQuery] string direction = "incoming", [FromQuery] string? status = "pending", [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Ok(Success(await _service.GetInvitesAsync(CurrentUserId, direction, status, page, pageSize), "Sprint invites retrieved."));

    [HttpPost("matchmaking")]
    public async Task<IActionResult> JoinMatchmaking(JoinPvpMatchmakingRequest request) => Ok(Success(await _service.JoinMatchmakingAsync(CurrentUserId, request), "Matchmaking request processed."));

    [HttpGet("matchmaking/status")]
    public async Task<IActionResult> GetMatchmakingStatus() => Ok(Success(await _service.GetMatchmakingStatusAsync(CurrentUserId), "Matchmaking status retrieved."));

    [HttpDelete("matchmaking")]
    public async Task<IActionResult> CancelMatchmaking() { await _service.CancelMatchmakingAsync(CurrentUserId); return Ok(Success<object?>(null, "Matchmaking cancelled.")); }

    [HttpGet("matches")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? matchType = null, [FromQuery] string? result = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] bool includeActive = false) => Ok(Success(await _service.GetHistoryAsync(CurrentUserId, page, pageSize, matchType, result, from, to, includeActive), "Sprint history retrieved."));

    [HttpGet("matches/{matchId:guid}")]
    public async Task<IActionResult> GetMatch(Guid matchId) => Ok(Success(await _service.GetMatchAsync(CurrentUserId, matchId), "Sprint match retrieved."));

    [HttpGet("matches/{matchId:guid}/result")]
    public async Task<IActionResult> GetResult(Guid matchId) => Ok(Success(await _service.GetResultAsync(CurrentUserId, matchId), "Sprint result retrieved."));

    [HttpPost("matches/{matchId:guid}/step-session")]
    public async Task<IActionResult> CreateStepSession(Guid matchId, CreatePvpStepSessionRequest request) => Ok(Success(await _service.CreateStepSessionAsync(CurrentUserId, matchId, request), "Sprint step session created."));

    [HttpPost("matches/{matchId:guid}/step-sessions/{sessionId:guid}/batches")]
    public async Task<IActionResult> SubmitStepBatch(Guid matchId, Guid sessionId, SubmitPvpStepBatchRequest request) => Ok(Success(await _service.SubmitStepBatchAsync(CurrentUserId, matchId, sessionId, request), "Sprint step batch accepted."));

    [HttpPost("matches/{matchId:guid}/reward-claim")]
    public async Task<IActionResult> ClaimReward(Guid matchId) => Ok(Success(await _service.ClaimRewardAsync(CurrentUserId, matchId), "Sprint reward claimed."));

    [HttpGet("loadout")]
    public async Task<IActionResult> GetLoadout() => Ok(Success(await _service.GetLoadoutAsync(CurrentUserId), "PvP loadout retrieved."));

    [HttpPut("loadout")]
    public async Task<IActionResult> UpdateLoadout(UpdatePvpLoadoutRequest request) => Ok(Success(await _service.UpdateLoadoutAsync(CurrentUserId, request), "PvP loadout updated."));

    [HttpPost("matches/{matchId:guid}/items/use")]
    public async Task<IActionResult> UseItem(Guid matchId, UsePvpItemRequest request) => Ok(Success(await _service.UseItemAsync(CurrentUserId, matchId, request), "PvP item action processed."));

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile() => Ok(Success(await _service.GetProfileAsync(CurrentUserId), "PvP profile retrieved."));

    [HttpGet("rankings")]
    public async Task<IActionResult> GetRankings([FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Ok(Success(await _service.GetRankingsAsync(CurrentUserId, page, pageSize), "PvP rankings retrieved."));

    private static ApiResponse<T> Success<T>(T data, string message) => new() { Success = true, Status = StatusCodes.Status200OK, Message = message, Data = data };
}
