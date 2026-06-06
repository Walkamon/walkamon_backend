using System.Security.Cryptography;
using System.Text;
using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BLL.Service;

public class AuthService : IAuthService
{
    private const string VerifyEmailPurposeCode = "verify_email";
    private const string UserRoleCode = "0";
    private static readonly string[] PolicySettingKeys =
    [
        "otp_verify_email_expire_minutes",
        "otp_max_attempts",
        "otp_resend_cooldown_seconds",
        "otp_send_ip_window_minutes",
        "otp_send_ip_max_count",
        "pending_registration_ttl_hours"
    ];

    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;

    public AuthService(
        IUserRepository userRepository,
        IEmailSender emailSender,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    public async Task<OtpSentResponse> RegisterAsync(
        RegisterRequest request,
        string requestedIp)
    {
        var email = request.Email.Trim();
        var username = request.Username.Trim();

        var normalizedEmail = email.ToUpperInvariant();
        var policy = await GetPolicyAsync();
        var now = DateTime.UtcNow;

        await _userRepository.CleanupExpiredPendingRegistrationsAsync(
            now.AddHours(-policy.PendingRegistrationTtlHours));

        var user = await _userRepository.GetUserByNormalizedEmailAsync(normalizedEmail);
        if (user != null && !IsPendingRegistration(user))
        {
            throw new ConflictException("Email already exists");
        }

        if (await _userRepository.UsernameExistsAsync(username, user?.UserId))
        {
            throw new ConflictException("Username already exists");
        }

        var pendingOtp = user == null
            ? null
            : await _userRepository.GetLatestPendingEmailVerificationOtpAsync(user.UserId);

        if (pendingOtp != null)
        {
            EnsureCooldownElapsed(pendingOtp.CreatedAt, now, policy.ResendCooldownSeconds);
            CancelOtp(pendingOtp, now);
            await _userRepository.SaveChangesAsync();
        }

        await EnsureIpRateLimitAsync(requestedIp, now, policy);

        if (user == null)
        {
            var role = await _userRepository.GetRoleByCodeAsync(UserRoleCode);
            if (role == null)
            {
                throw new AppSystemException("Default user role is not configured");
            }

            user = CreatePendingUser(
                role.RoleId,
                email,
                normalizedEmail,
                username,
                request.Password);
            await _userRepository.AddAsync(user);
        }
        else
        {
            UpdatePendingUser(user, email, username, request.Password, now);
        }

        var (otp, plaintextOtp) = CreateOtp(user, requestedIp, now, policy);

        try
        {
            await _userRepository.AddOtpAsync(otp);
            await _userRepository.SaveChangesAsync();
        }

        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new ConflictException("Email or username already exists");
        }

        await SendOtpOrCancelAsync(otp, user.Email, plaintextOtp, policy.ExpiryMinutes);
        return ToOtpSentResponse(otp, policy.ResendCooldownSeconds);
    }

    public async Task<RegisterResponse> VerifyRegistrationOtpAsync(
        VerifyRegistrationOtpRequest request)
    {
        var otp = await _userRepository.GetOtpRequestAsync(request.RequestCode);
        if (otp == null
            || otp.PurposeCode != VerifyEmailPurposeCode
            || otp.StatusCode != "pending"
            || !IsPendingRegistration(otp.User))
        {
            throw new BadRequestException("OTP request is invalid");
        }

        var now = DateTime.UtcNow;
        if (otp.ExpiresAt <= now)
        {
            otp.StatusCode = "expired";
            otp.UpdatedAt = now;
            await _userRepository.SaveChangesAsync();
            throw new BadRequestException("OTP has expired");
        }

        if (!CryptographicOperations.FixedTimeEquals(otp.OtpHash, HashOtp(request.Otp)))
        {
            otp.AttemptCount++;
            otp.UpdatedAt = now;
            if (otp.AttemptCount >= otp.MaxAttempts)
            {
                otp.StatusCode = "cancelled";
            }

            await _userRepository.SaveChangesAsync();
            throw new BadRequestException("OTP is invalid");
        }

        otp.StatusCode = "verified";
        otp.UsedAt = now;
        otp.UpdatedAt = now;
        otp.User.StatusCode = "active";
        otp.User.EmailConfirmed = true;
        otp.User.UpdatedAt = now;
        await _userRepository.SaveChangesAsync();

        return new RegisterResponse
        {
            UserId = otp.User.UserId,
            Email = otp.User.Email,
            Username = otp.User.UserProfile!.Username!
        };
    }

    public async Task<OtpSentResponse?> ResendRegistrationOtpAsync(
        ResendRegistrationOtpRequest request,
        string requestedIp)
    {
        var policy = await GetPolicyAsync();
        var now = DateTime.UtcNow;

        await _userRepository.CleanupExpiredPendingRegistrationsAsync(
            now.AddHours(-policy.PendingRegistrationTtlHours));

        var otp = await _userRepository.GetOtpRequestAsync(request.RequestCode);
        if (otp == null
            || otp.PurposeCode != VerifyEmailPurposeCode
            || otp.StatusCode is not ("pending" or "expired")
            || !IsPendingRegistration(otp.User))
        {
            return null;
        }

        EnsureCooldownElapsed(otp.CreatedAt, now, policy.ResendCooldownSeconds);
        await EnsureIpRateLimitAsync(requestedIp, now, policy);

        if (otp.StatusCode == "pending")
        {
            CancelOtp(otp, now);
            await _userRepository.SaveChangesAsync();
        }

        var (replacement, plaintextOtp) = CreateOtp(otp.User, requestedIp, now, policy);
        await _userRepository.AddOtpAsync(replacement);
        await _userRepository.SaveChangesAsync();
        await SendOtpOrCancelAsync(
            replacement,
            otp.User.Email,
            plaintextOtp,
            policy.ExpiryMinutes);

        return ToOtpSentResponse(replacement, policy.ResendCooldownSeconds);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);

        if (user == null)
        {
            throw new NotFoundException("User not found");
        }

        if (!user.EmailConfirmed &&
    user.StatusCode.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Account is not activated");
        }

        if (user.StatusCode.Equals("disabled", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Account has been locked");
        }

        if (user.LockoutEndAt.HasValue && user.LockoutEndAt > DateTime.UtcNow)
        {
            var remainingMinutes = Math.Max(
                1,
                (int)Math.Ceiling((user.LockoutEndAt.Value - DateTime.UtcNow).TotalMinutes));

            throw new BadRequestException(
                $"Account is locked. Try again after {remainingMinutes} minute(s)");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.AccessFailedCount++;

            if (user.AccessFailedCount >= 5)
            {
                user.LockoutEndAt = DateTime.UtcNow.AddMinutes(5);
                _userRepository.Update(user);
                await _userRepository.SaveAsync();

                throw new BadRequestException(
                    "Account locked for 5 minutes because too many failed login attempts");
            }

            _userRepository.Update(user);
            await _userRepository.SaveAsync();

            throw new BadRequestException(
                $"Wrong password. Remaining attempts: {5 - user.AccessFailedCount}");
        }

        user.AccessFailedCount = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _userRepository.SaveAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.RoleName)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new LoginResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            Role = user.Role.RoleName,
            Jwt = new JwtSecurityTokenHandler().WriteToken(token)
        };
    }

    private static User CreatePendingUser(
        int roleId,
        string email,
        string normalizedEmail,
        string username,
        string password)
    {
        return new User
        {
            RoleId = roleId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            EmailConfirmed = false,
            StatusCode = "active",
            UserProfile = new UserProfile
            {
                Username = username,
                LanguageCode = "vi-VN",
                ThemeCode = "light",
                TimeZoneId = "Asia/Ho_Chi_Minh",
                ShowActivityStats = true,
                NotificationsEnabled = true
            }
        };
    }

    private static void UpdatePendingUser(
        User user,
        string email,
        string username,
        string password,
        DateTime now)
    {
        user.Email = email;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        user.UpdatedAt = now;
        user.UserProfile!.Username = username;
        user.UserProfile.UpdatedAt = now;
    }

    private static (OtpRequest Otp, string PlaintextOtp) CreateOtp(
        User user,
        string requestedIp,
        DateTime now,
        OtpPolicy policy)
    {
        var plaintextOtp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        var otp = new OtpRequest
        {
            User = user,
            PurposeCode = VerifyEmailPurposeCode,
            TargetValue = user.Email,
            OtpHash = HashOtp(plaintextOtp),
            RequestCode = Guid.NewGuid(),
            ExpiresAt = now.AddMinutes(policy.ExpiryMinutes),
            MaxAttempts = (short)policy.MaxAttempts,
            StatusCode = "pending",
            RequestedIp = requestedIp,
            CreatedAt = now,
            UpdatedAt = now
        };

        return (otp, plaintextOtp);
    }

    private async Task SendOtpOrCancelAsync(
        OtpRequest otp,
        string email,
        string plaintextOtp,
        int expiryMinutes)
    {
        try
        {
            await _emailSender.SendRegistrationOtpAsync(email, plaintextOtp, expiryMinutes);
        }
        catch
        {
            CancelOtp(otp, DateTime.UtcNow);
            await _userRepository.SaveChangesAsync();
            throw new AppSystemException("Unable to send verification email");
        }
    }

    private async Task<OtpPolicy> GetPolicyAsync()
    {
        var settings = await _userRepository.GetSystemSettingsAsync(PolicySettingKeys);

        return new OtpPolicy(
            ReadPositiveInt(settings, "otp_verify_email_expire_minutes"),
            ReadPositiveInt(settings, "otp_max_attempts"),
            ReadPositiveInt(settings, "otp_resend_cooldown_seconds"),
            ReadPositiveInt(settings, "otp_send_ip_window_minutes"),
            ReadPositiveInt(settings, "otp_send_ip_max_count"),
            ReadPositiveInt(settings, "pending_registration_ttl_hours"));
    }

    private async Task EnsureIpRateLimitAsync(
        string requestedIp,
        DateTime now,
        OtpPolicy policy)
    {
        var count = await _userRepository.CountRecentEmailVerificationOtpsByIpAsync(
            requestedIp,
            now.AddMinutes(-policy.IpWindowMinutes));

        if (count >= policy.IpMaxCount)
        {
            throw new TooManyRequestsException(
                "Too many OTP requests. Please try again later",
                policy.IpWindowMinutes * 60);
        }
    }

    private static int ReadPositiveInt(
        IReadOnlyDictionary<string, string> settings,
        string key)
    {
        if (!settings.TryGetValue(key, out var value)
            || !int.TryParse(value, out var parsed)
            || parsed <= 0)
        {
            throw new AppSystemException($"System setting {key} is not configured");
        }

        return parsed;
    }

    private static void EnsureCooldownElapsed(
        DateTime createdAt,
        DateTime now,
        int cooldownSeconds)
    {
        var retryAfterSeconds = (int)Math.Ceiling(
            (createdAt.AddSeconds(cooldownSeconds) - now).TotalSeconds);

        if (retryAfterSeconds > 0)
        {
            throw new TooManyRequestsException(
                "Please wait before requesting another OTP",
                retryAfterSeconds);
        }
    }

    private static void CancelOtp(OtpRequest otp, DateTime now)
    {
        otp.StatusCode = "cancelled";
        otp.UpdatedAt = now;
    }

    private static bool IsPendingRegistration(User user)
    {
        return user.StatusCode == "active" && !user.EmailConfirmed;
    }

    private static OtpSentResponse ToOtpSentResponse(OtpRequest otp, int cooldownSeconds)
    {
        return new OtpSentResponse
        {
            RequestCode = otp.RequestCode,
            ExpiresAtUtc = otp.ExpiresAt,
            ResendAvailableAtUtc = otp.CreatedAt.AddSeconds(cooldownSeconds)
        };
    }

    private static byte[] HashOtp(string otp)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(otp));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Number is 2601 or 2627;
    }

    private sealed record OtpPolicy(
        int ExpiryMinutes,
        int MaxAttempts,
        int ResendCooldownSeconds,
        int IpWindowMinutes,
        int IpMaxCount,
        int PendingRegistrationTtlHours);
}
