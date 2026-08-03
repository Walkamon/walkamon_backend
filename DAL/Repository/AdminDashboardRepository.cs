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
    var now = DateTime.UtcNow.AddHours(7);

    var today = DateOnly.FromDateTime(now);
    var yesterday = today.AddDays(-1);

    var todayStart = today.ToDateTime(TimeOnly.MinValue);

    var currentMonth = new DateTime(now.Year, now.Month, 1);
    var previousMonth = currentMonth.AddMonths(-1);

    //------------------------------------------------------
    // USER
    //------------------------------------------------------

    var totalUsers = await _context.Users.CountAsync();

    var newUsersToday = await _context.Users
        .CountAsync(x => x.CreatedAt >= todayStart);

    var newUsersThisMonth = await _context.Users
        .CountAsync(x => x.CreatedAt >= currentMonth);

    var previousUsers = await _context.Users
        .CountAsync(x =>
            x.CreatedAt >= previousMonth &&
            x.CreatedAt < currentMonth);

    double growth = previousUsers == 0
        ? (newUsersThisMonth == 0 ? 0 : 100)
        : Math.Round(
            (newUsersThisMonth - previousUsers) * 100.0 / previousUsers,
            2);

    //------------------------------------------------------
    // STEP
    //------------------------------------------------------

    var totalSteps = await _context.DailySteps
        .SumAsync(x => (long?)x.StepCount) ?? 0;

    var stepsToday = await _context.DailySteps
        .Where(x => x.StepDate == today)
        .SumAsync(x => (long?)x.StepCount) ?? 0;

    double averageSteps = await _context.DailySteps.AnyAsync()
        ? await _context.DailySteps.AverageAsync(x => x.StepCount)
        : 0;

    double averageStepsPerUser = totalUsers == 0
        ? 0
        : Math.Round((double)totalSteps / totalUsers, 2);

    //------------------------------------------------------
    // WALKING
    //------------------------------------------------------

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

    double compare = walkingYesterday == 0
        ? (walkingToday == 0 ? 0 : 100)
        : Math.Round(
            walkingToday * 100.0 / walkingYesterday,
            2);

    //------------------------------------------------------
    // PET
    //------------------------------------------------------

    var totalPets = await _context.UserPets.CountAsync();

    double averagePetLevel = await _context.UserPets.AnyAsync()
        ? await _context.UserPets.AverageAsync(x => x.Level)
        : 0;

    int highestPetLevel = await _context.UserPets.AnyAsync()
        ? await _context.UserPets.MaxAsync(x => x.Level)
        : 0;

    //------------------------------------------------------
    // INTERACTION
    //------------------------------------------------------

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

    //------------------------------------------------------

    return new DashboardResponseDto
    {
        TotalUsers = totalUsers,

        NewUsersToday = newUsersToday,

        NewUsersThisMonth = newUsersThisMonth,

        UserGrowthPercentage = growth,

        TotalSteps = totalSteps,

        StepsToday = stepsToday,

        AverageStepsPerDay = Math.Round(averageSteps, 0),

        AverageStepsPerUser = averageStepsPerUser,

        WalkingUsersToday = walkingToday,

        CompareWithYesterday = compare,

        TotalPets = totalPets,

        AveragePetLevel = Math.Round(averagePetLevel, 2),

        HighestPetLevel = highestPetLevel,

        PetInteractions = petInteractions
    };
}
    }
}
