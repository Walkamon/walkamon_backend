using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/pvp/sprint")]
public sealed class AdminPvpSprintController : ControllerBase
{
    private readonly IPvpSprintService _service;
    public AdminPvpSprintController(IPvpSprintService service) => _service = service;

    [HttpGet("reward-rules")]
    public async Task<IActionResult> Get() => Ok(new ApiResponse<List<PvpRewardRuleResponse>> { Success = true, Status = StatusCodes.Status200OK, Message = "Sprint reward rules retrieved.", Data = await _service.GetRewardRulesAsync() });

    [HttpPut("reward-rules")]
    public async Task<IActionResult> Update(UpdatePvpRewardRulesRequest request)
    {
        await _service.UpdateRewardRulesAsync(request);
        return Ok(new ApiResponse<object?> { Success = true, Status = StatusCodes.Status200OK, Message = "Sprint reward rules updated.", Data = null });
    }

    [HttpGet("item-effects")]
    public async Task<IActionResult> GetItemEffects() => Ok(new ApiResponse<List<PvpItemEffectAdminRequest>> { Success = true, Status = StatusCodes.Status200OK, Message = "PvP item effects retrieved.", Data = await _service.GetItemEffectsAsync() });

    [HttpPut("item-effects")]
    public async Task<IActionResult> UpdateItemEffects(UpdatePvpItemEffectsRequest request) { await _service.UpdateItemEffectsAsync(request); return Ok(new ApiResponse<object?> { Success = true, Status = StatusCodes.Status200OK, Message = "PvP item effects updated.", Data = null }); }

    [HttpGet("spirit-rules")]
    public async Task<IActionResult> GetSpiritRules() => Ok(new ApiResponse<List<PvpSpiritRuleAdminRequest>> { Success = true, Status = StatusCodes.Status200OK, Message = "PvP spirit rules retrieved.", Data = await _service.GetSpiritRulesAsync() });

    [HttpPut("spirit-rules")]
    public async Task<IActionResult> UpdateSpiritRules(UpdatePvpSpiritRulesRequest request) { await _service.UpdateSpiritRulesAsync(request); return Ok(new ApiResponse<object?> { Success = true, Status = StatusCodes.Status200OK, Message = "PvP spirit rules updated.", Data = null }); }

    [HttpGet("rank-tiers")]
    public async Task<IActionResult> GetRankTiers() => Ok(new ApiResponse<List<PvpRankTierAdminRequest>> { Success = true, Status = StatusCodes.Status200OK, Message = "PvP rank tiers retrieved.", Data = await _service.GetRankTiersAsync() });

    [HttpPut("rank-tiers")]
    public async Task<IActionResult> UpdateRankTiers(UpdatePvpRankTiersRequest request) { await _service.UpdateRankTiersAsync(request); return Ok(new ApiResponse<object?> { Success = true, Status = StatusCodes.Status200OK, Message = "PvP rank tiers updated.", Data = null }); }
}
