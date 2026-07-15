using DAL.Data;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository;

public class DailyLoginRewardRepository : IDailyLoginRewardRepository
{
    private readonly WalkamonContext _context;

    public DailyLoginRewardRepository(WalkamonContext context)
    {
        _context = context;
    }

    public Task<DailyLoginRewardClaim?> GetLatestClaimAsync(Guid userId)
    {
        return _context.DailyLoginRewardClaims
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.ClaimDate)
            .FirstOrDefaultAsync();
    }

    public Task<Wallet?> GetWalletAsync(Guid userId)
    {
        return _context.Wallets.FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public Task AddClaimAsync(DailyLoginRewardClaim claim)
    {
        return _context.DailyLoginRewardClaims.AddAsync(claim).AsTask();
    }

    public void UpdateWallet(Wallet wallet)
    {
        _context.Wallets.Update(wallet);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
