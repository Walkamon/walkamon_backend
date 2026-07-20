using DAL.Data;
using DAL.GenericRepository;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(WalkamonContext context)
            : base(context)
        {
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.Email == email);
        }

        public Task<User?> GetUserByNormalizedEmailAsync(string normalizedEmail)
        {
            return _context.Users
                .Include(user => user.UserProfile)
                .Where(user => user.NormalizedEmail == normalizedEmail)
                .OrderByDescending(user => user.DeletedAt == null)
                .ThenByDescending(user => user.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public Task<bool> UsernameExistsAsync(
            string username,
            Guid? excludedUserId = null)
        {
            var normalizedUsername = username.ToUpperInvariant();

            return _context.UserProfiles.AnyAsync(profile =>
                profile.Username != null
                && profile.Username.ToUpper() == normalizedUsername
                && (!excludedUserId.HasValue || profile.UserId != excludedUserId.Value));
        }

        public Task<Role?> GetRoleByCodeAsync(string roleCode)
        {
            return _context.Roles
                .SingleOrDefaultAsync(role => role.RoleCode == roleCode);
        }

        public Task<OtpRequest?> GetOtpRequestAsync(Guid requestCode)
        {
            return _context.OtpRequests
                .Include(otp => otp.User)
                    .ThenInclude(user => user.UserProfile)
                .SingleOrDefaultAsync(otp => otp.RequestCode == requestCode);
        }

        public Task<OtpRequest?> GetLatestPendingEmailVerificationOtpAsync(Guid userId)
        {
            return _context.OtpRequests
                .Where(otp =>
                    otp.UserId == userId
                    && otp.PurposeCode == "verify_email"
                    && otp.StatusCode == "pending")
                .OrderByDescending(otp => otp.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public Task<int> CountRecentEmailVerificationOtpsByIpAsync(
            string requestedIp,
            DateTime createdAfterUtc)
        {
            return _context.OtpRequests.CountAsync(otp =>
                otp.PurposeCode == "verify_email"
                && otp.RequestedIp == requestedIp
                && otp.CreatedAt >= createdAfterUtc);
        }

        public async Task<IReadOnlyDictionary<string, string>> GetSystemSettingsAsync(
            IEnumerable<string> settingKeys)
        {
            var keys = settingKeys.ToArray();

            return await _context.SystemSettings
                .Where(setting => keys.Contains(setting.SettingKey))
                .ToDictionaryAsync(
                    setting => setting.SettingKey,
                    setting => setting.SettingValue);
        }

        public async Task CleanupExpiredPendingRegistrationsAsync(DateTime createdBeforeUtc)
        {
            var otpRequests = await _context.OtpRequests
                .Where(otp =>
                    otp.PurposeCode == "verify_email"
                    && otp.StatusCode == "pending"
                    && otp.CreatedAt < createdBeforeUtc
                    && otp.User.StatusCode == "active"
                    && !otp.User.EmailConfirmed)
                .ToListAsync();

            if (otpRequests.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var otpRequest in otpRequests)
            {
                otpRequest.StatusCode = "cancelled";
                otpRequest.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
        }

        public Task AddOtpAsync(OtpRequest otpRequest)
        {
            return _context.OtpRequests.AddAsync(otpRequest).AsTask();
        }

        public Task SaveChangesAsync()
        {
            return _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _context.Users
        .Include(x => x.UserProfile)
        .ToListAsync();
        }

        public async Task<User?> GetByIdWithProfileAsync(Guid id)
        {
            return await _context.Users
                .Include(x => x.UserProfile)
                .FirstOrDefaultAsync(x => x.UserId == id);
        }

        public async Task<User?> GetUserWithRoleAsync(Guid id)
        {
            return await _context.Users
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.UserId == id);
        }

        public async Task<Guid?> GetRequestCodeByUserIdAsync(Guid userId)
        {
            return await _context.OtpRequests
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (Guid?)x.RequestCode)
                .FirstOrDefaultAsync();
        }
    }
}
