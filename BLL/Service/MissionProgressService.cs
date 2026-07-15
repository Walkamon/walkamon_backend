using BLL.Interfaces;
using DAL.Data;
using DAL.Extensions;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Service;

public class MissionProgressService : IMissionProgressService
{
    private const string DailyMissionTypeCode = "daily";
    private const string OverallMissionTypeCode = "overall";
    private const string ActiveStatusCode = "active";
    private const string ClaimedStatusCode = "claimed";

    private readonly WalkamonContext _context;

    public MissionProgressService(WalkamonContext context)
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
        var now = DateTime.UtcNow;
        var missions = await _context.Missions
            .Where(x => x.MetricCode == metricCode
                && x.IsActive
                && (!x.StartAt.HasValue || x.StartAt.Value <= now)
                && (!x.EndAt.HasValue || x.EndAt.Value >= now))
            .ToListAsync();

        if (missions.Count == 0) return;

        await _context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
        {
            foreach (var mission in missions)
            {
                if (!await ArePrerequisitesMetAsync(userId, mission.MissionId))
                {
                    continue;
                }

                var cycleDate = GetCycleDate(mission.MissionTypeCode, now);

                var userMission = await _context.UserMissions
                    .FromSqlInterpolated($@"
                        SELECT * FROM user_missions WITH (UPDLOCK, HOLDLOCK)
                        WHERE user_id = {userId} 
                          AND mission_id = {mission.MissionId} 
                          AND cycle_date = {cycleDate}
                    ")
                    .SingleOrDefaultAsync();

                if (userMission == null)
                {
                    userMission = new UserMission
                    {
                        UserMissionId = Guid.NewGuid(),
                        UserId = userId,
                        MissionId = mission.MissionId,
                        CycleDate = cycleDate,
                        StatusCode = ActiveStatusCode,
                        ProgressValue = isIncremental ? valueOrAmount : valueOrAmount,
                        AssignedAt = now
                    };
                    await _context.UserMissions.AddAsync(userMission);
                }
                else
                {
                    if (isIncremental)
                    {
                        if ((long)userMission.ProgressValue + valueOrAmount > int.MaxValue)
                        {
                            userMission.ProgressValue = int.MaxValue;
                        }
                        else
                        {
                            userMission.ProgressValue += valueOrAmount;
                        }
                    }
                    else
                    {
                        userMission.ProgressValue = Math.Max(userMission.ProgressValue, valueOrAmount);
                    }
                    _context.UserMissions.Update(userMission);
                }
            }

            await _context.SaveChangesAsync();
        });
    }

    public async Task<bool> ArePrerequisitesMetAsync(Guid userId, Guid missionId)
    {
        var assignmentConditions = await _context.MissionConditions
            .Where(c => c.MissionId == missionId && c.ConditionGroup == "assignment")
            .ToListAsync();

        if (assignmentConditions.Count == 0)
        {
            return true;
        }

        var now = DateTime.UtcNow;

        foreach (var condition in assignmentConditions)
        {
            if (condition.ReferenceMissionId.HasValue)
            {
                var refMissionId = condition.ReferenceMissionId.Value;
                var refMission = await _context.Missions.FindAsync(refMissionId);

                if (refMission == null)
                {
                    return false;
                }

                var cycleDate = GetCycleDate(refMission.MissionTypeCode, now);
                var userRefMission = await _context.UserMissions
                    .FirstOrDefaultAsync(x => x.UserId == userId
                        && x.MissionId == refMissionId
                        && x.CycleDate == cycleDate);

                if (userRefMission == null || !string.Equals(userRefMission.StatusCode, ClaimedStatusCode, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static DateOnly GetCycleDate(string missionTypeCode, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);

        return missionTypeCode switch
        {
            DailyMissionTypeCode => today,
            OverallMissionTypeCode => DateOnly.MinValue,
            _ => today
        };
    }
}
