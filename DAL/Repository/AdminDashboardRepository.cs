using DAL.Data;
using DAL.DTO;
using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
namespace DAL.Repository
{
    public class AdminDashboardRepository : IAdminDashboardRepository
    {
        private readonly WalkamonContext _context;

        public AdminDashboardRepository(WalkamonContext context)
        {
            _context = context;
        }

        public async Task<DashboardResponseDto> GetDashboardAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var yesterday = today.AddDays(-1);

            var now = DateTime.UtcNow.AddHours(7);

            var currentMonth = new DateTime(now.Year, now.Month, 1);
            var previousMonth = currentMonth.AddMonths(-1);

            //--------------------------------------------------
            // Users
            //--------------------------------------------------

            var totalUsers = await _context.Users.CountAsync();

            var currentUsers = await _context.Users
                .CountAsync(x => x.CreatedAt >= currentMonth);

            var previousUsers = await _context.Users
                .CountAsync(x =>
                    x.CreatedAt >= previousMonth &&
                    x.CreatedAt < currentMonth);

            double growth = 0;

            if (previousUsers == 0)
            {
                growth = currentUsers == 0 ? 0 : 100;
            }
            else
            {
                growth = Math.Round(
                    (currentUsers - previousUsers) * 100.0 / previousUsers,
                    2);
            }

            //--------------------------------------------------
            // Steps
            //--------------------------------------------------

            var totalSteps = await _context.DailySteps
                .SumAsync(x => (long?)x.StepCount) ?? 0;

            var averageSteps = await _context.DailySteps.AnyAsync()
                ? await _context.DailySteps.AverageAsync(x => x.StepCount)
                : 0;

            //--------------------------------------------------
            // Active
            //--------------------------------------------------

            var walkingToday = await _context.DailySteps
                .Where(x => x.StepDate == today)
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync();

            var walkingYesterday = await _context.DailySteps
                .Where(x => x.StepDate == yesterday)
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync();

            double compare = 0;

            if (walkingYesterday == 0)
            {
                compare = walkingToday == 0 ? 0 : 100;
            }
            else
            {
                compare = Math.Round(
                    walkingToday * 100.0 / walkingYesterday,
                    2);
            }

            //--------------------------------------------------
            // Feed Tap
            //--------------------------------------------------

            var interactions = await _context.PetInteractions

                .Where(x =>
                    x.InteractionType == "Feed" ||
                    x.InteractionType == "Tap")

                .GroupBy(x => x.InteractionType)

                .Select(g => new
                {
                    Type = g.Key,
                    Total = g.Sum(x => x.Count)
                })

                .ToListAsync();

            var interactionTotal = interactions.Sum(x => x.Total);

            var petInteractions = interactions

                .Select(x => new PetInteractionStatisticDto
                {
                    InteractionType = x.Type,

                    TotalCount = x.Total,

                    Percentage = interactionTotal == 0
                        ? 0
                        : Math.Round(
                            x.Total * 100.0 / interactionTotal,
                            2)
                })

                .OrderByDescending(x => x.TotalCount)

                .ToList();

            //--------------------------------------------------

            return new DashboardResponseDto
            {
                TotalUsers = totalUsers,

                ActiveUsers = walkingToday,

                TotalSteps = totalSteps,

                UserGrowthPercentage = growth,

                AverageStepsPerDay = Math.Round(averageSteps, 0),

                WalkingUsersToday = walkingToday,

                CompareWithYesterday = compare,

                PetInteractions = petInteractions
            };
        }
    }
}
