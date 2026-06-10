using BLL.Interfaces;
using DAL.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Walkamon.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : BaseController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        return Ok(new ApiResponse<LoginResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Login success",
            Data = result
        });
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

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<OtpSentResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var result = await _authService.ForgotPasswordAsync(request, GetRequestedIp());

        return StatusCode(
            StatusCodes.Status202Accepted,
            new ApiResponse<OtpSentResponse>
            {
                Success = true,
                Status = StatusCodes.Status202Accepted,
                Message = "If the account is valid, a password reset OTP has been sent",
                Data = result
            });
    }
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {


        await _authService.LogoutAsync(CurrentUserId);

        return Ok(new
        {
            Message = "Logout successfully"
        });
    }

    [HttpPost("forgot-password/reset")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetForgotPassword(
        ResetForgotPasswordRequest request)
    {
        await _authService.ResetForgotPasswordAsync(request);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Password reset success",
            Data = null
        });
    }

    private string GetRequestedIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
