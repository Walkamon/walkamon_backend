using BLL.Exceptions;
using BLL.Interfaces;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Service
{
    public class DailyStepService : IDailyStepService
    {
        private readonly IDailyStepRepository _dailyStepRepository;
        private readonly IGenericRepository<DailyStep> _repository;
        private readonly IAchievementProgressService _achievementProgressService;
        private readonly IMissionProgressService _missionProgressService;

        public DailyStepService(
            IDailyStepRepository dailyStepRepository,
            IGenericRepository<DailyStep> repository,
            IAchievementProgressService achievementProgressService,
            IMissionProgressService missionProgressService)
        {
            _dailyStepRepository = dailyStepRepository;
            _repository = repository;
            _achievementProgressService = achievementProgressService;
            _missionProgressService = missionProgressService;
        }

        public async Task UpdateStepAsync(
    Guid userId,
    UpdateDailyStepRequest request)
        {
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);

            var today = DateOnly.FromDateTime(vietnamNow);

            var dailyStep = await _dailyStepRepository
                .GetByUserAndDateAsync(userId, today);

            if (dailyStep == null)
            {
                dailyStep = new DailyStep
                {
                    UserId = userId,
                    StepDate = today,
                    StepCount = request.StepCount,
                    UpdatedAt = vietnamNow
                };

                await _repository.AddAsync(dailyStep);
            }
            else
            {
                dailyStep.StepCount += request.StepCount;
                dailyStep.UpdatedAt = vietnamNow;
            }

            await _repository.SaveAsync();
            
            await _achievementProgressService.AddProgressAsync(userId, "steps", request.StepCount);
            await _missionProgressService.AddProgressAsync(userId, "steps", request.StepCount);
        }

        public async Task<DailyStepStatisticDto> GetDailyStatisticAsync(
     Guid userId,
     DateOnly? date)
        {
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

            var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                vietnamTimeZone);

            var selectedDate = date ?? DateOnly.FromDateTime(vietnamNow);

            var step = await _dailyStepRepository.GetByDateAsync(
                userId,
                selectedDate);

            return new DailyStepStatisticDto
            {
                Type = "Daily",

                FromDate = selectedDate,

                ToDate = selectedDate,

                Data = new List<DailyStepStatisticItemDto>
        {
            new DailyStepStatisticItemDto
            {
                Label = selectedDate.ToString("dd/MM"),

                StepCount = step?.StepCount ?? 0
            }
        }
            };
        }

        public async Task<DailyStepStatisticDto> GetWeeklyStatisticAsync(
    Guid userId,
    DateOnly? date)
        {
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

            var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                vietnamTimeZone);

            var selectedDate = date ?? DateOnly.FromDateTime(vietnamNow);

            int diff = (7 + (selectedDate.DayOfWeek - DayOfWeek.Monday)) % 7;

            var startDate = selectedDate.AddDays(-diff);
            var endDate = startDate.AddDays(6);

            var steps = await _dailyStepRepository.GetByDateRangeAsync(
                userId,
                startDate,
                endDate);

            var result = new List<DailyStepStatisticItemDto>();

            for (var day = startDate; day <= endDate; day = day.AddDays(1))
            {
                var step = steps.FirstOrDefault(x => x.StepDate == day);

                result.Add(new DailyStepStatisticItemDto
                {
                    Label = day.DayOfWeek.ToString(),
                    StepCount = step?.StepCount ?? 0
                });
            }

            return new DailyStepStatisticDto
            {
                Type = "Weekly",
                FromDate = startDate,
                ToDate = endDate,
                Data = result
            };
        }

        public async Task<DailyStepStatisticDto> GetMonthlyStatisticAsync(
    Guid userId,
    DateOnly? date)
        {
             var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

   var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(
       DateTime.UtcNow,
       vietnamTimeZone);

   var selectedDate = date ?? DateOnly.FromDateTime(vietnamNow);

   var firstDay = new DateOnly(selectedDate.Year, 1, 1);
   var lastDay = new DateOnly(selectedDate.Year, 12, 31);

   var steps = await _dailyStepRepository.GetByDateRangeAsync(
       userId,
       firstDay,
       lastDay);

   var monthlySteps = steps
       .GroupBy(x => x.StepDate.Month)
       .ToDictionary(
           g => g.Key,
           g => g.Sum(x => x.StepCount));

   var result = new List<DailyStepStatisticItemDto>();

   for (int month = 1; month <= 12; month++)
   {
       result.Add(new DailyStepStatisticItemDto
       {
           Label = month.ToString(), 
           StepCount = monthlySteps.GetValueOrDefault(month, 0)
       });
   }

   return new DailyStepStatisticDto
   {
       Type = "Monthly",
       FromDate = firstDay,
       ToDate = lastDay,
       Data = result
   };
        }

        public async Task<LeaderboardDto> GetLeaderboardAsync(
    Guid currentUserId,
    LeaderboardType type,
    DateOnly? date)
        {
            var selectedDate = GetSelectedDate(date);

            DateOnly fromDate;
            DateOnly toDate;

            switch (type)
            {
                case LeaderboardType.Daily:
                    fromDate = selectedDate;
                    toDate = selectedDate;
                    break;

                case LeaderboardType.Weekly:

                    int diff = (7 + (selectedDate.DayOfWeek - DayOfWeek.Monday)) % 7;

                    fromDate = selectedDate.AddDays(-diff);

                    toDate = fromDate.AddDays(6);

                    break;

                case LeaderboardType.Monthly:

                    fromDate = new DateOnly(
                        selectedDate.Year,
                        selectedDate.Month,
                        1);

                    toDate = fromDate
                        .AddMonths(1)
                        .AddDays(-1);

                    break;

                default:
                    throw new BadRequestException("Leaderboard type is invalid.");
            }

            var rawLeaderboard = await _dailyStepRepository.GetLeaderboardAsync(
                fromDate,
                toDate);

            var leaderboard = rawLeaderboard
                .Select((x, index) => new LeaderboardUserDto
                {
                    Rank = index + 1,

                    UserId = x.UserId,

                    Username = x.Username,

                    Avatar = x.Avatar,

                    StepCount = x.StepCount,

                    IsCurrentUser = x.UserId == currentUserId
                })
                .ToList();

            var myRank = leaderboard
                .FirstOrDefault(x => x.IsCurrentUser)?.Rank ?? 0;

            return new LeaderboardDto
            {
                Type = type.ToString(),

                FromDate = fromDate,

                ToDate = toDate,

                MyRank = myRank,

                Leaderboard = leaderboard.Take(100).ToList()
            };
        }

        private DateOnly GetSelectedDate(DateOnly? date)
        {
            if (date.HasValue)
                return date.Value;

            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

            var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                vietnamTimeZone);

            return DateOnly.FromDateTime(vietnamNow);
        }
    }
}
