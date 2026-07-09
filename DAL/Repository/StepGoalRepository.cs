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
            return await _context.DailySteps
                .Where(ds => ds.UserId == userId)
                .Where(ds =>
                    ds.StepCount >= _context.StepGoals
                        .Where(g => g.UserId == ds.UserId &&
                                    g.EffectiveFrom <= ds.StepDate)
                        .OrderByDescending(g => g.EffectiveFrom)
                        .Select(g => (int?)g.TargetSteps)
                        .FirstOrDefault())
                .ToListAsync();
        }
        public async Task<List<StepGoalHistoryResponse>> GetStepGoalHistoryAsync(Guid userId)
        {
            return await _context.StepGoals
                .Where(g => g.UserId == userId)
                .Select(g => new StepGoalHistoryResponse
                {
                    GoalDate = g.EffectiveFrom,
                    TargetSteps = g.TargetSteps,

                    CompletedSteps = _context.DailySteps
                        .Where(s =>
                            s.UserId == g.UserId &&
                            s.StepDate == g.EffectiveFrom)
                        .Select(s => s.StepCount)
                        .FirstOrDefault(),

                    IsCompleted =
                        _context.DailySteps
                            .Where(s =>
                                s.UserId == g.UserId &&
                                s.StepDate == g.EffectiveFrom)
                            .Select(s => s.StepCount)
                            .FirstOrDefault() >= g.TargetSteps
                })
                .OrderByDescending(x => x.GoalDate)
                .ToListAsync();
        }
    }
}
