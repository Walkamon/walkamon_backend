using DAL.Models;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace DAL.Interfaces;

public interface IDailyLoginRewardRepository
{
    Task<DailyLoginRewardClaim?> GetLatestClaimAsync(Guid userId);

    Task<Wallet?> GetWalletAsync(Guid userId);

    Task AddClaimAsync(DailyLoginRewardClaim claim);

    void UpdateWallet(Wallet wallet);

    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel);

    Task SaveChangesAsync();
}
