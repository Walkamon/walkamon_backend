using DAL.Data;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace DAL.Repository
{
    public class DailyStepRepository : IDailyStepRepository
    {
        private readonly WalkamonContext _context;

        public DailyStepRepository(WalkamonContext context)
        {
            _context = context;
        }

        public async Task<DailyStep?> GetByUserAndDateAsync(
            Guid userId,
            DateOnly date)
        {
            return await _context.DailySteps
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.StepDate == date);
        }

        public async Task<DailyStep?> GetByDateAsync(Guid userId, DateOnly date)
        {
            return await _context.DailySteps
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.StepDate == date);
        }

        public async Task<List<DailyStep>> GetByDateRangeAsync(
    Guid userId,
    DateOnly fromDate,
    DateOnly toDate)
        {
            return await _context.DailySteps
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.StepDate >= fromDate &&
                    x.StepDate <= toDate)
                .OrderBy(x => x.StepDate)
                .ToListAsync();
        }
        public async Task<List<LeaderboardRawDto>> GetLeaderboardAsync(
    DateOnly fromDate,
    DateOnly toDate)
        {
            return await _context.DailySteps
                .Where(x =>
                    x.StepDate >= fromDate &&
                    x.StepDate <= toDate &&
                    x.User.StatusCode == "active")
                .GroupBy(x => new
                {
                    x.UserId,
                    Username = x.User.UserProfile!.Username,
                    Avatar = x.User.UserProfile.AvatarUrl
                })
                .Select(g => new LeaderboardRawDto
                {
                    UserId = g.Key.UserId,
                    Username = g.Key.Username,
                    Avatar = g.Key.Avatar,
                    StepCount = g.Sum(x => x.StepCount)
                })
                .OrderByDescending(x => x.StepCount)
                .ToListAsync();
        }
    }
}
