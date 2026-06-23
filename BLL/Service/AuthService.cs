using System.Security.Cryptography;
using System.Text;
using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using Google.Apis.Auth;
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
    private const string ForgotPasswordPurposeCode = "forgot_password";
    private const string UserRoleCode = "0";
    private const string GoogleProviderName = "google";
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
    private readonly IGenericRepository<OtpRequest> _otpRepository;
    private readonly IGenericRepository<Wallet> _walletRepository;
    private readonly IGenericRepository<ExternalLogin> _externalLoginRepository;

    public AuthService(
        IUserRepository userRepository,
        IGenericRepository<OtpRequest> otpRepository,
        IGenericRepository<Wallet> walletRepository,
        IGenericRepository<ExternalLogin> externalLoginRepository,
        IEmailSender emailSender,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _otpRepository = otpRepository;
        _walletRepository = walletRepository;
        _externalLoginRepository = externalLoginRepository;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    // Đăng ký: tạo user chờ xác thực và gửi OTP.
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

        var (otp, plaintextOtp) = CreateOtp(
            user,
            VerifyEmailPurposeCode,
            requestedIp,
            now,
            policy);

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

    // OTP đăng ký: xác thực mã và kích hoạt tài khoản.
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

        var wallet = await _walletRepository.GetByIdAsync(otp.User.UserId);
        if (wallet == null)
        {
            await _walletRepository.AddAsync(new Wallet
            {
                UserId = otp.User.UserId,
                Balance = 0
            });
        }

        await _userRepository.SaveChangesAsync();

        return new RegisterResponse
        {
            UserId = otp.User.UserId,
            Email = otp.User.Email,
            Username = otp.User.UserProfile!.Username!
        };
    }

    // OTP đăng ký: gửi lại mã xác thực email.
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

        var (replacement, plaintextOtp) = CreateOtp(
            otp.User,
            VerifyEmailPurposeCode,
            requestedIp,
            now,
            policy);
        await _userRepository.AddOtpAsync(replacement);
        await _userRepository.SaveChangesAsync();
        await SendOtpOrCancelAsync(
            replacement,
            otp.User.Email,
            plaintextOtp,
            policy.ExpiryMinutes);

        return ToOtpSentResponse(replacement, policy.ResendCooldownSeconds);
    }

    // Quên mật khẩu: gửi OTP đặt lại mật khẩu.
    public async Task<OtpSentResponse?> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        string requestedIp)
    {
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        var policy = await GetPolicyAsync();
        var now = DateTime.UtcNow;

        var user = await _userRepository.GetUserByNormalizedEmailAsync(normalizedEmail);
        if (user == null
            || user.DeletedAt != null
            || !user.EmailConfirmed
            || !user.StatusCode.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var otpRequests = await _otpRepository.GetAllAsync();
        var recentRequestCount = otpRequests.Count(otp =>
            otp.PurposeCode == ForgotPasswordPurposeCode
            && otp.RequestedIp == requestedIp
            && otp.CreatedAt >= now.AddMinutes(-policy.IpWindowMinutes));

        if (recentRequestCount >= policy.IpMaxCount)
        {
            throw new TooManyRequestsException(
                "Too many OTP requests. Please try again later",
                policy.IpWindowMinutes * 60);
        }

        var pendingOtp = otpRequests
            .Where(otp =>
                otp.UserId == user.UserId
                && otp.PurposeCode == ForgotPasswordPurposeCode
                && otp.StatusCode == "pending")
            .OrderByDescending(otp => otp.CreatedAt)
            .FirstOrDefault();

        if (pendingOtp != null)
        {
            EnsureCooldownElapsed(pendingOtp.CreatedAt, now, policy.ResendCooldownSeconds);
            CancelOtp(pendingOtp, now);
            await _otpRepository.SaveAsync();
        }

        var (otp, plaintextOtp) = CreateOtp(
            user,
            ForgotPasswordPurposeCode,
            requestedIp,
            now,
            policy);

        await _otpRepository.AddAsync(otp);
        await _otpRepository.SaveAsync();
        await SendOtpOrCancelAsync(otp, user.Email, plaintextOtp, policy.ExpiryMinutes);

        return ToOtpSentResponse(otp, policy.ResendCooldownSeconds);
    }

    // Quên mật khẩu: xác thực OTP và đổi mật khẩu mới.
    public async Task ResetForgotPasswordAsync(ResetForgotPasswordRequest request)
    {
        var otp = await _userRepository.GetOtpRequestAsync(request.RequestCode);
        if (otp == null
            || otp.PurposeCode != ForgotPasswordPurposeCode
            || otp.StatusCode != "pending"
            || otp.User.DeletedAt != null
            || !otp.User.EmailConfirmed
            || !otp.User.StatusCode.Equals("active", StringComparison.OrdinalIgnoreCase))
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
        otp.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        otp.User.PasswordChangedAt = now;
        otp.User.AccessFailedCount = 0;
        otp.User.LockoutEndAt = null;
        otp.User.UpdatedAt = now;
        await _userRepository.SaveChangesAsync();
    }

    // Đổi mật khẩu: user tự đổi bằng mật khẩu hiện tại.
    public async Task ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request)
    {
        var user = await _userRepository.GetUserWithRoleAsync(userId);

        if (user == null)
        {
            throw new NotFoundException("User not found");
        }

        if (user.Role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Admin accounts cannot change password");
        }

        if (user.StatusCode.Equals("disabled", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Account has been locked");
        }

        if (!user.EmailConfirmed)
        {
            throw new BadRequestException("Account is not activated");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash)
            || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new BadRequestException("Current password is invalid");
        }

        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
        {
            throw new BadRequestException(
                "New password must be different from current password");
        }

        var now = DateTime.UtcNow;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordChangedAt = now;
        user.AccessFailedCount = 0;
        user.LockoutEndAt = null;
        user.UpdatedAt = now;

        _userRepository.Update(user);
        await _userRepository.SaveAsync();
    }

    // Đăng nhập: kiểm tra mật khẩu và cấp JWT.
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
            var requestCode = await _userRepository.GetRequestCodeByUserIdAsync(user.UserId);
        
            throw new NotActiveException(requestCode);
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

        if (string.IsNullOrWhiteSpace(user.PasswordHash)
            || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
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

        return CreateLoginResponse(user, user.Role.RoleName);
    }

    public async Task<LoginResponse> GoogleLoginAsync(GoogleLoginRequest request)
    {
        var payload = await ValidateGoogleTokenAsync(request.IdToken);
        if (string.IsNullOrWhiteSpace(payload.Subject)
            || string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new BadRequestException("Invalid Google token");
        }

        if (!payload.EmailVerified)
        {
            throw new BadRequestException("Google email is not verified");
        }

        var now = DateTime.UtcNow;
        var email = payload.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        var externalLogin = await _externalLoginRepository.FirstOrDefaultAsync(login =>
            login.ProviderName == GoogleProviderName
            && login.ProviderSubject == payload.Subject);

        if (externalLogin != null)
        {
            var linkedUser = await _userRepository.GetUserWithRoleAsync(externalLogin.UserId);
            if (linkedUser == null)
            {
                throw new BadRequestException("Invalid Google login");
            }

            EnsureGoogleLoginAllowed(linkedUser);

            externalLogin.ProviderEmail = email;
            externalLogin.ProviderDisplayName = payload.Name;
            externalLogin.LastLoginAt = now;
            linkedUser.LastLoginAt = now;
            linkedUser.UpdatedAt = now;

            await EnsureWalletExistsAsync(linkedUser.UserId);
            _externalLoginRepository.Update(externalLogin);
            _userRepository.Update(linkedUser);
            await _userRepository.SaveChangesAsync();

            return CreateLoginResponse(linkedUser, linkedUser.Role.RoleName);
        }

        var user = await _userRepository.GetUserByNormalizedEmailAsync(normalizedEmail);
        string roleName;

        if (user == null)
        {
            var role = await _userRepository.GetRoleByCodeAsync(UserRoleCode);
            if (role == null)
            {
                throw new AppSystemException("Default user role is not configured");
            }

            user = new User
            {
                UserId = Guid.NewGuid(),
                RoleId = role.RoleId,
                Email = email,
                NormalizedEmail = normalizedEmail,
                PasswordHash = null,
                EmailConfirmed = true,
                StatusCode = "active",
                AccessFailedCount = 0,
                LockoutEndAt = null,
                LastLoginAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                UserProfile = new UserProfile
                {
                    Username = await CreateUniqueGoogleUsernameAsync(payload.Name, email),
                    LanguageCode = "vi-VN",
                    ThemeCode = "light",
                    TimeZoneId = "Asia/Ho_Chi_Minh",
                    ShowActivityStats = true,
                    NotificationsEnabled = true,
                    CreatedAt = now,
                    UpdatedAt = now
                }
            };

            roleName = role.RoleName;
            await _userRepository.AddAsync(user);
        }
        else
        {
            EnsureGoogleLoginAllowed(user);

            user.Email = email;
            user.NormalizedEmail = normalizedEmail;
            user.EmailConfirmed = true;
            user.StatusCode = "active";
            user.LastLoginAt = now;
            user.UpdatedAt = now;

            if (user.UserProfile == null)
            {
                user.UserProfile = new UserProfile
                {
                    Username = await CreateUniqueGoogleUsernameAsync(payload.Name, email),
                    LanguageCode = "vi-VN",
                    ThemeCode = "light",
                    TimeZoneId = "Asia/Ho_Chi_Minh",
                    ShowActivityStats = true,
                    NotificationsEnabled = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
            }

            _userRepository.Update(user);
            var userWithRole = await _userRepository.GetUserWithRoleAsync(user.UserId);
            roleName = userWithRole?.Role.RoleName ?? "User";
        }

        await EnsureWalletExistsAsync(user.UserId);
        await _externalLoginRepository.AddAsync(new ExternalLogin
        {
            UserId = user.UserId,
            ProviderName = GoogleProviderName,
            ProviderSubject = payload.Subject,
            ProviderEmail = email,
            ProviderDisplayName = payload.Name,
            CreatedAt = now,
            LastLoginAt = now
        });

        await _userRepository.SaveChangesAsync();
        return CreateLoginResponse(user, roleName);
    }

    private async Task<GoogleJsonWebSignature.Payload> ValidateGoogleTokenAsync(
        string idToken)
    {
        var clientIds = _configuration
            .GetSection("GoogleAuth:ClientIds")
            .Get<string[]>()
            ?.Where(clientId => !string.IsNullOrWhiteSpace(clientId))
            .ToArray();

        if (clientIds == null || clientIds.Length == 0)
        {
            var singleClientId = _configuration["GoogleAuth:ClientId"];
            clientIds = string.IsNullOrWhiteSpace(singleClientId)
                ? []
                : [singleClientId];
        }

        if (clientIds.Length == 0)
        {
            throw new AppSystemException("Google client id is not configured");
        }

        try
        {
            return await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = clientIds
                });
        }
        catch (Exception exception) when (
            exception is InvalidJwtException
            || exception is ArgumentException)
        {
            throw new BadRequestException("Invalid Google token");
        }
    }

    private async Task EnsureWalletExistsAsync(Guid userId)
    {
        var wallet = await _walletRepository.GetByIdAsync(userId);
        if (wallet != null)
        {
            return;
        }

        await _walletRepository.AddAsync(new Wallet
        {
            UserId = userId,
            Balance = 0
        });
    }

    private static void EnsureGoogleLoginAllowed(User user)
    {
        if (user.DeletedAt != null
            || user.StatusCode.Equals("disabled", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Account has been locked");
        }
    }

    private async Task<string> CreateUniqueGoogleUsernameAsync(
        string? displayName,
        string email)
    {
        var source = string.IsNullOrWhiteSpace(displayName)
            ? email.Split('@')[0]
            : displayName;

        var baseUsername = new string(source
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(baseUsername))
        {
            baseUsername = "user";
        }

        if (baseUsername.Length > 30)
        {
            baseUsername = baseUsername[..30];
        }

        var username = baseUsername;
        var suffix = 1;

        while (await _userRepository.UsernameExistsAsync(username))
        {
            var suffixText = suffix.ToString();
            var prefixLength = Math.Min(baseUsername.Length, 30 - suffixText.Length);
            username = $"{baseUsername[..prefixLength]}{suffixText}";
            suffix++;
        }

        return username;
    }

    private LoginResponse CreateLoginResponse(User user, string roleName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, roleName)
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
            Role = roleName,
            Jwt = new JwtSecurityTokenHandler().WriteToken(token)
        };
    }

    // Đăng ký: tạo user pending trước khi xác thực OTP.
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

    // Đăng ký: cập nhật lại user pending khi đăng ký lại.
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

    // OTP: tạo mã 6 số và lưu hash.
    private static (OtpRequest Otp, string PlaintextOtp) CreateOtp(
        User user,
        string purposeCode,
        string requestedIp,
        DateTime now,
        OtpPolicy policy)
    {
        var plaintextOtp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        var otp = new OtpRequest
        {
            User = user,
            PurposeCode = purposeCode,
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

    // OTP: gửi mail, lỗi thì hủy OTP.
    private async Task SendOtpOrCancelAsync(
        OtpRequest otp,
        string email,
        string plaintextOtp,
        int expiryMinutes)
    {
        try
        {
            if (otp.PurposeCode == ForgotPasswordPurposeCode)
            {
                await _emailSender.SendPasswordResetOtpAsync(
                    email,
                    plaintextOtp,
                    expiryMinutes);
            }
            else
            {
                await _emailSender.SendRegistrationOtpAsync(
                    email,
                    plaintextOtp,
                    expiryMinutes);
            }
        }
        catch
        {
            CancelOtp(otp, DateTime.UtcNow);
            await _userRepository.SaveChangesAsync();
            throw new AppSystemException("Unable to send verification email");
        }
    }

    // OTP: đọc cấu hình thời hạn, cooldown và giới hạn gửi.
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

    // OTP: giới hạn số lần gửi theo IP.
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

    // Đăng ký: kiểm tra user còn đang chờ xác thực email.
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

    // Đăng xuất: ghi lại thời điểm logout gần nhất.
    public async Task LogoutAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
            throw new NotFoundException("User not found");

        user.LastLogoutAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _userRepository.SaveAsync();
    }

    private sealed record OtpPolicy(
        int ExpiryMinutes,
        int MaxAttempts,
        int ResendCooldownSeconds,
        int IpWindowMinutes,
        int IpMaxCount,
        int PendingRegistrationTtlHours);
}
