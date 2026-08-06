using BLL.Interfaces;
using DAL.DTO;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Walkamon.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : BaseController
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IStepGoalService _streakService;
    public AuthController(
        IAuthService authService,
        IUserService userService,
        IStepGoalService stepGoalService,
        ICloudinaryService cloudinaryService)
    {
        _authService = authService;
        _userService = userService;
        _cloudinaryService = cloudinaryService;
        _streakService = stepGoalService;
    }

    // Đăng nhập.
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

    // Đăng nhập bằng Google idToken từ Flutter.
    [HttpPost("google-login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request)
    {
        var result = await _authService.GoogleLoginAsync(request);

        return Ok(new ApiResponse<LoginResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Google login success",
            Data = result
        });
    }

    // Đăng ký và gửi OTP xác thực email.
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

    // Xác thực OTP đăng ký.
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

    // Gửi lại OTP đăng ký.
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

    // Quên mật khẩu: gửi OTP đặt lại mật khẩu.
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

    [HttpPost("forgot-password/verify")]
    [ProducesResponseType(typeof(ApiResponse<ForgotPasswordResetTicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyForgotPasswordOtp(
        VerifyForgotPasswordOtpRequest request)
    {
        var result = await _authService.VerifyForgotPasswordOtpAsync(request);

        return Ok(new ApiResponse<ForgotPasswordResetTicketResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "OTP verified",
            Data = result
        });
    }

    [HttpPost("forgot-password/reset-with-ticket")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetForgotPasswordWithTicket(
        ResetForgotPasswordWithTicketRequest request)
    {
        await _authService.ResetForgotPasswordWithTicketAsync(request);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Password reset success",
            Data = null
        });
    }

    // Xem profile của user hiện tại.
    [Authorize]
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _userService.GetUserByIdAsync(CurrentUserId);

        if (user == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Status = StatusCodes.Status404NotFound,
                Message = "User not found",
                Data = null
            });
        }

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get profile success",
            Data = new
            {
                Username = user.Profile?.Username,
                user.Email,
                Gender = user.Profile?.Gender,
                Bio = user.Profile?.Bio,
                Dob = user.Profile?.Dob,
                AvatarUrl = user.Profile?.AvatarUrl,
                HasSeenStory = user.Profile?.HasSeenStory,
                LanguageCode = user.Profile?.LanguageCode,
                ThemeCode = user.Profile?.ThemeCode,
                TimeZoneId = user.Profile?.TimeZoneId,
                ShowActivityStats = user.Profile?.ShowActivityStats,
                NotificationsEnabled = user.Profile?.NotificationsEnabled,
                CreatedAt = user.Profile?.CreatedAt,
                UpdatedAt = user.Profile?.UpdatedAt
            }
        });
    }

    // Cập nhật profile và ảnh đại diện.
    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileRequest request)
    {
        string? avatarUrl = null;
        if (request.Image != null)
        {
            avatarUrl = await _cloudinaryService.UploadImageAsync(request.Image);
        }

        var user = await _userService.UpdateProfileAsync(
            CurrentUserId,
            request,
            avatarUrl);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update profile success",
            Data = new
            {
                Username = user.Profile?.Username,
                user.Email,
                Gender = user.Profile?.Gender,
                Bio = user.Profile?.Bio,
                Dob = user.Profile?.Dob,
                AvatarUrl = user.Profile?.AvatarUrl,
                HasSeenStory = user.Profile?.HasSeenStory,
                LanguageCode = user.Profile?.LanguageCode,
                ThemeCode = user.Profile?.ThemeCode,
                TimeZoneId = user.Profile?.TimeZoneId,
                ShowActivityStats = user.Profile?.ShowActivityStats,
                NotificationsEnabled = user.Profile?.NotificationsEnabled,
                CreatedAt = user.Profile?.CreatedAt,
                UpdatedAt = user.Profile?.UpdatedAt
            }
        });
    }

    [Authorize(Roles = "User")]
    [HttpPatch("profile/language")]
    [ProducesResponseType(typeof(ApiResponse<UserPreferenceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLanguageMode(
        UpdateLanguageModeRequest request)
    {
        var result = await _userService.UpdateLanguageModeAsync(
            CurrentUserId,
            request);

        return Ok(new ApiResponse<UserPreferenceResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update language mode success",
            Data = result
        });
    }

    [Authorize(Roles = "User")]
    [HttpPatch("profile/theme")]
    [ProducesResponseType(typeof(ApiResponse<UserPreferenceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateThemeMode(
        UpdateThemeModeRequest request)
    {
        var result = await _userService.UpdateThemeModeAsync(
            CurrentUserId,
            request);

        return Ok(new ApiResponse<UserPreferenceResponse>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Update theme mode success",
            Data = result
        });
    }

    // Đổi mật khẩu, không cho phép Admin.
    [Authorize]
    [HttpPut("change-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        await _authService.ChangePasswordAsync(CurrentUserId, request);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Change password success",
            Data = null
        });
    }

    // Đăng xuất.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {


        await _authService.LogoutAsync(CurrentUserId);

        return Ok(new
        {
         Success = true,
 Status = StatusCodes.Status200OK,
            Message = "Logout successfully"
        });
    }

    // Xác thực OTP quên mật khẩu và đặt mật khẩu mới.
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
    [HttpGet("profile-friend/{userId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [HttpGet("other-profile/{userId}")]
    public async Task<IActionResult> GetOtherProfile(Guid userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Status = StatusCodes.Status404NotFound,
                Message = "User not found",
                Data = null
            });
        }

        var longestStreak = await _streakService.GetLongestStreakAsync(userId);
        var currentStreak = await _streakService.GetCurrentStreakAsync(userId);
        var streakHistory = await _streakService.GetStreakHistoryAsync(userId);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Status = StatusCodes.Status200OK,
            Message = "Get profile success",
            Data = new
            {
                Username = user.Profile?.Username,
                Gender = user.Profile?.Gender,
                Bio = user.Profile?.Bio,
                Dob = user.Profile?.Dob,
                AvatarUrl = user.Profile?.AvatarUrl,

                CurrentStreak = currentStreak,
                LongestStreak = longestStreak,
                StreakHistory = streakHistory
            }
        });
    }
    private string GetRequestedIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
