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

        private static DateTime VietNamNow()
            => DateTime.UtcNow.AddHours(7);

        private static DateOnly VietNamToday()
            => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        //====================================================

        public async Task<int> GetTotalUsersAsync()
        {
            return await _context.Users.CountAsync();
        }

        //====================================================

        public async Task<int> GetActiveUsersTodayAsync()
        {
            var today = VietNamToday();

            return await _context.DailySteps
                .Where(x => x.StepDate == today)
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync();
        }

        //====================================================

        public async Task<long> GetTotalStepsAsync()
        {
            return await _context.DailySteps
                .SumAsync(x => (long)x.StepCount);
        }

        //====================================================

        public async Task<double> GetUserGrowthPercentageAsync()
        {
            var now = VietNamNow();

            var currentMonth = new DateTime(now.Year, now.Month, 1);

            var previousMonth = currentMonth.AddMonths(-1);

            var current = await _context.Users
                .CountAsync(x => x.CreatedAt >= currentMonth);

            var previous = await _context.Users
                .CountAsync(x =>
                    x.CreatedAt >= previousMonth &&
                    x.CreatedAt < currentMonth);

            if (previous == 0)
                return current == 0 ? 0 : 100;

            return Math.Round(
                (current - previous) * 100.0 / previous,
                2);
        }

        //====================================================

        public async Task<double> GetAverageStepsPerDayAsync()
        {
            if (!await _context.DailySteps.AnyAsync())
                return 0;

            return await _context.DailySteps
                .AverageAsync(x => x.StepCount);
        }

        //====================================================

        public async Task<int> GetWalkingUsersTodayAsync()
        {
            var today = VietNamToday();

            return await _context.DailySteps
                .Where(x => x.StepDate == today)
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync();
        }

        //====================================================

        public async Task<double> GetCompareWithYesterdayAsync()
        {
            var today = VietNamToday();

            var yesterday = today.AddDays(-1);

            var todayUsers = await _context.DailySteps
                .Where(x => x.StepDate == today)
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync();

            var yesterdayUsers = await _context.DailySteps
                .Where(x => x.StepDate == yesterday)
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync();

            if (yesterdayUsers == 0)
                return todayUsers == 0 ? 0 : 100;

            return Math.Round(
                todayUsers * 100.0 / yesterdayUsers,
                2);
        }

        //====================================================

        public async Task<List<PetInteractionStatisticDto>> GetPetInteractionStatisticsAsync()
        {
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

            var total = interactions.Sum(x => x.Total);

            return interactions
                .Select(x => new PetInteractionStatisticDto
                {
                    InteractionType = x.Type,
                    TotalCount = x.Total,
                    Percentage = total == 0
                        ? 0
                        : Math.Round(
                            x.Total * 100.0 / total,
                            2)
                })
                .OrderByDescending(x => x.TotalCount)
                .ToList();
        }
    }
}
