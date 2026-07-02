using DAL.Data;
using DAL.Interfaces;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
namespace DAL.Repository
{
    public class StreakRewardRepository : IStreakRewardRepository
    {
        private readonly WalkamonContext _context;

        public StreakRewardRepository(WalkamonContext context)
        {
            _context = context;
        }

        public async Task<bool> HasClaimedTodayAsync(Guid userId, DateOnly today)
        {
            return await _context.StreakRewardClaims
                .AnyAsync(x =>
                    x.UserId == userId &&
                    x.ClaimDate == today);
        }

       
    }
}
