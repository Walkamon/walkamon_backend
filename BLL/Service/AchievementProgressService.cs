using BLL.Interfaces;
using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Service;

public class AchievementProgressService : IAchievementProgressService
{
    private readonly WalkamonContext _context;

    public AchievementProgressService(WalkamonContext context)
    {
        _context = context;
    }

    public async Task AddProgressAsync(Guid userId, string metricCode, int amount)
    {
        if (amount <= 0) return;
        await UpdateProgressAsync(userId, metricCode, amount, isIncremental: true);
    }

    public async Task SetProgressMaxAsync(Guid userId, string metricCode, int value)
    {
        await UpdateProgressAsync(userId, metricCode, value, isIncremental: false);
    }

    private async Task UpdateProgressAsync(Guid userId, string metricCode, int valueOrAmount, bool isIncremental)
    {
        var achievements = await _context.Achievements
            .Where(x => x.MetricCode == metricCode && x.IsActive)
            .ToListAsync();

        if (achievements.Count == 0) return;

        // Registration verification already owns an execution-strategy transaction.
        // Reuse it so SQL Server does not receive a nested user transaction.
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        try
        {
            var newlyUnlockedIds = new List<Guid>();

            foreach (var ach in achievements)
            {
                var userAch = await _context.UserAchievements
                    .FromSqlInterpolated($@"
                        SELECT * FROM user_achievements WITH (UPDLOCK, HOLDLOCK)
                        WHERE user_id = {userId} AND achievement_id = {ach.AchievementId}
                    ")
                    .SingleOrDefaultAsync();

                if (userAch == null)
                {
                    userAch = new UserAchievement
                    {
                        UserId = userId,
                        AchievementId = ach.AchievementId,
                        ProgressValue = isIncremental ? valueOrAmount : valueOrAmount
                    };
                    await _context.UserAchievements.AddAsync(userAch);
                }
                else
                {
                    if (isIncremental)
                    {
                        if ((long)userAch.ProgressValue + valueOrAmount > int.MaxValue)
                        {
                            userAch.ProgressValue = int.MaxValue;
                        }
                        else
                        {
                            userAch.ProgressValue += valueOrAmount;
                        }
                    }
                    else
                    {
                        userAch.ProgressValue = Math.Max(userAch.ProgressValue, valueOrAmount);
                    }
                    _context.UserAchievements.Update(userAch);
                }

                if (!userAch.UnlockedAt.HasValue && userAch.ProgressValue >= ach.TargetValue)
                {
                    if (await ArePrerequisitesMetAsync(userId, ach.AchievementId))
                    {
                        userAch.UnlockedAt = DateTime.UtcNow;
                        newlyUnlockedIds.Add(ach.AchievementId);
                    }
                }
            }

            await _context.SaveChangesAsync();

            if (newlyUnlockedIds.Count > 0)
            {
                await ProcessCascadingUnlocksAsync(userId, newlyUnlockedIds);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            throw;
        }
    }

    private async Task<bool> ArePrerequisitesMetAsync(Guid userId, Guid achievementId)
    {
        var conditions = await _context.AchievementConditions
            .Where(c => c.AchievementId == achievementId && c.ConditionGroup == "assignment")
            .ToListAsync();

        if (conditions.Count == 0) return true;

        foreach (var c in conditions)
        {
            if (!c.ReferenceAchievementId.HasValue) continue;

            var userRefAch = _context.UserAchievements.Local.FirstOrDefault(x => x.UserId == userId && x.AchievementId == c.ReferenceAchievementId.Value);

            if (userRefAch == null)
            {
                userRefAch = await _context.UserAchievements
                    .FromSqlInterpolated($@"
                        SELECT * FROM user_achievements WITH (UPDLOCK, HOLDLOCK)
                        WHERE user_id = {userId} AND achievement_id = {c.ReferenceAchievementId.Value}
                    ")
                    .SingleOrDefaultAsync();
            }

            if (userRefAch == null || !userRefAch.UnlockedAt.HasValue)
            {
                return false;
            }
        }

        return true;
    }

    private async Task ProcessCascadingUnlocksAsync(Guid userId, List<Guid> recentlyUnlockedAchievementIds)
    {
        var queue = new Queue<Guid>(recentlyUnlockedAchievementIds);

        while (queue.Count > 0)
        {
            var unlockedId = queue.Dequeue();

            var dependentConditionAchIds = await _context.AchievementConditions
                .Where(c => c.ConditionGroup == "assignment" && c.ReferenceAchievementId == unlockedId)
                .Select(c => c.AchievementId)
                .Distinct()
                .ToListAsync();

            if (dependentConditionAchIds.Count == 0) continue;

            var dependentAchs = await _context.Achievements
                .Where(a => dependentConditionAchIds.Contains(a.AchievementId) && a.IsActive)
                .ToListAsync();

            foreach (var dependentAch in dependentAchs)
            {
                var userAch = _context.UserAchievements.Local.FirstOrDefault(x => x.UserId == userId && x.AchievementId == dependentAch.AchievementId);

                if (userAch == null)
                {
                    userAch = await _context.UserAchievements
                        .FromSqlInterpolated($@"
                            SELECT * FROM user_achievements WITH (UPDLOCK, HOLDLOCK)
                            WHERE user_id = {userId} AND achievement_id = {dependentAch.AchievementId}
                        ")
                        .SingleOrDefaultAsync();
                }

                if (userAch != null && !userAch.UnlockedAt.HasValue && userAch.ProgressValue >= dependentAch.TargetValue)
                {
                    if (await ArePrerequisitesMetAsync(userId, dependentAch.AchievementId))
                    {
                        userAch.UnlockedAt = DateTime.UtcNow;
                        _context.UserAchievements.Update(userAch);
                        await _context.SaveChangesAsync();
                        queue.Enqueue(dependentAch.AchievementId);
                    }
                }
            }
        }
    }
}
