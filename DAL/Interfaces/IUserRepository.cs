using DAL.Models;

namespace DAL.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByNormalizedEmailAsync(string normalizedEmail);

    Task<bool> UsernameExistsAsync(string normalizedUsername, Guid? excludedUserId = null);

    Task<Role?> GetRoleByCodeAsync(string roleCode);

    Task<OtpRequest?> GetOtpRequestAsync(Guid requestCode);

    Task<OtpRequest?> GetLatestPendingEmailVerificationOtpAsync(Guid userId);

    Task<int> CountRecentEmailVerificationOtpsByIpAsync(
        string requestedIp,
        DateTime createdAfterUtc);

    Task<IReadOnlyDictionary<string, string>> GetSystemSettingsAsync(
        IEnumerable<string> settingKeys);

    Task CleanupExpiredPendingRegistrationsAsync(DateTime createdBeforeUtc);

    Task AddAsync(User user);

    Task AddOtpAsync(OtpRequest otpRequest);

    Task SaveChangesAsync();
}
