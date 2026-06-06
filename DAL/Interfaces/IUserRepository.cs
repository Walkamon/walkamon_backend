    using DAL.Models;

    namespace DAL.Interfaces;

    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);

        Task<User?> GetUserByNormalizedEmailAsync(string normalizedEmail);

        Task<bool> UsernameExistsAsync(string username, Guid? excludedUserId = null);

        Task<Role?> GetRoleByCodeAsync(string roleCode);

        Task<OtpRequest?> GetOtpRequestAsync(Guid requestCode);

        Task<OtpRequest?> GetLatestPendingEmailVerificationOtpAsync(Guid userId);

        Task<int> CountRecentEmailVerificationOtpsByIpAsync(
            string requestedIp,
            DateTime createdAfterUtc);

        Task<IReadOnlyDictionary<string, string>> GetSystemSettingsAsync(
            IEnumerable<string> settingKeys);

        Task CleanupExpiredPendingRegistrationsAsync(DateTime createdBeforeUtc);

        Task AddOtpAsync(OtpRequest otpRequest);

    Task SaveChangesAsync();

    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User?> GetByIdWithProfileAsync(Guid id);
    Task<User?> GetUserWithRoleAsync(Guid id);
}
