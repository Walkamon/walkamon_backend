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
                .SingleOrDefaultAsync(user =>
                    user.NormalizedEmail == normalizedEmail
                    && user.DeletedAt == null);
        }

        public Task<bool> UsernameExistsAsync(
            string normalizedUsername,
            Guid? excludedUserId = null)
        {
            return _context.UserProfiles.AnyAsync(profile =>
                profile.NormalizedUsername == normalizedUsername
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
            var users = await _context.Users
                .Where(user =>
                    user.StatusCode == "disabled"
                    && !user.EmailConfirmed
                    && user.CreatedAt < createdBeforeUtc)
                .Include(user => user.OtpRequests)
                .Include(user => user.UserProfile)
                .ToListAsync();

            if (users.Count == 0)
            {
                return;
            }

            _context.OtpRequests.RemoveRange(users.SelectMany(user => user.OtpRequests));

            _context.UserProfiles.RemoveRange(
                users
                    .Where(user => user.UserProfile != null)
                    .Select(user => user.UserProfile!));

            _context.Users.RemoveRange(users);

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
    }
}
