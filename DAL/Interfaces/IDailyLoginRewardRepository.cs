using DAL.Models;

namespace DAL.Interfaces;

public interface IDailyLoginRewardRepository
{
    Task<DailyLoginRewardClaim?> GetLatestClaimAsync(Guid userId);

    Task<Wallet?> GetWalletAsync(Guid userId);

    Task AddClaimAsync(DailyLoginRewardClaim claim);

    void UpdateWallet(Wallet wallet);

    Task SaveChangesAsync();
}
