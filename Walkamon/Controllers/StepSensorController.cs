using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/step-sensor")]
public sealed class StepSensorController : BaseController
{
    private readonly IValidatedStepService _service;

    public StepSensorController(IValidatedStepService service) => _service = service;

    [HttpPost("session")]
    public async Task<IActionResult> CreateSession(
        CreatePvpStepSessionRequest request,
        CancellationToken cancellationToken) =>
        Ok(Success(
            await _service.CreateDailySessionAsync(CurrentUserId, request, cancellationToken),
            "Daily physical-step session created."));

    [HttpPost("sessions/{sessionId:guid}/batches")]
    public async Task<IActionResult> SubmitBatch(
        Guid sessionId,
        SubmitPvpStepBatchRequest request,
        CancellationToken cancellationToken) =>
        Ok(Success(
            await _service.SubmitDailyBatchAsync(CurrentUserId, sessionId, request, cancellationToken),
            "Daily physical-step batch processed."));

    private static ApiResponse<T> Success<T>(T data, string message) => new()
    {
        Success = true,
        Status = StatusCodes.Status200OK,
        Message = message,
        Data = data
    };
}
