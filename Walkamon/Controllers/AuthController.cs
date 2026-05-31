using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Mvc;

namespace Walkamon.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<OtpSentResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request, GetRequestedIp());

        return StatusCode(
            StatusCodes.Status202Accepted,
            new ApiResponse<OtpSentResponse>
            {
                Success = true,
                Status = StatusCodes.Status202Accepted,
                Message = "OTP sent",
                Data = result
            });
    }

    [HttpPost("register/verify")]
    [ProducesResponseType(typeof(ApiResponse<RegisterResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyRegistrationOtp(VerifyRegistrationOtpRequest request)
    {
        var result = await _authService.VerifyRegistrationOtpAsync(request);

        return StatusCode(
            StatusCodes.Status201Created,
            new ApiResponse<RegisterResponse>
            {
                Success = true,
                Status = StatusCodes.Status201Created,
                Message = "Register success",
                Data = result
            });
    }

    [HttpPost("register/resend-otp")]
    [ProducesResponseType(typeof(ApiResponse<OtpSentResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResendRegistrationOtp(
        ResendRegistrationOtpRequest request)
    {
        var result = await _authService.ResendRegistrationOtpAsync(request, GetRequestedIp());

        return StatusCode(
            StatusCodes.Status202Accepted,
            new ApiResponse<OtpSentResponse>
            {
                Success = true,
                Status = StatusCodes.Status202Accepted,
                Message = "If the registration request is valid, a new OTP has been sent",
                Data = result
            });
    }

    private string GetRequestedIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
