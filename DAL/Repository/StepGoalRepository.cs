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
    public class StepGoalRepository : IStepGoalRepository
    {
        private readonly WalkamonContext _context;

        public StepGoalRepository(WalkamonContext context)
        {
            _context = context;
        }

        public async Task<StepGoal?> GetTodayGoalAsync(Guid userId, DateOnly today)
        {
            return await _context.StepGoals
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.EffectiveFrom == today);
        }

        public async Task<StepGoal?> GetCurrentGoalAsync(Guid userId, DateOnly date)
        {
            return await _context.StepGoals
                .Where(x =>
                    x.UserId == userId &&
                    x.EffectiveFrom <= date)
                .OrderByDescending(x => x.EffectiveFrom)
                .FirstOrDefaultAsync();
        }

        public async Task<DailyStep?> GetDailyStepAsync(
    Guid userId,
    DateOnly date)
        {
            return await _context.DailySteps
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.StepDate == date);
        }

        public async Task<List<DailyStep>> GetCompletedGoalHistoryAsync(Guid userId)
        {
            var goals = _context.StepGoals;

            return await _context.DailySteps
                .Where(ds => ds.UserId == userId)
                .Where(ds =>
                    ds.StepCount >= goals
                        .Where(g => g.UserId == ds.UserId &&
                                    g.EffectiveFrom <= ds.StepDate)
                        .OrderByDescending(g => g.EffectiveFrom)
                        .Select(g => g.TargetSteps)
                        .FirstOrDefault())
                .OrderBy(ds => ds.StepDate)
                .ToListAsync();
        }
    }
}
