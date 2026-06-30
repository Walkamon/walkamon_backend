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
    public class StepGoalService : IStepGoalService

    {
        private readonly IStepGoalRepository _stepGoalRepository;
        private readonly IGenericRepository<StepGoal> _repository;
        public StepGoalService(
    IStepGoalRepository stepGoalRepository,
    IGenericRepository<StepGoal> repository)
        {
            _stepGoalRepository = stepGoalRepository;
            _repository = repository;
        }
        public async Task SetGoalAsync(
     Guid currentUserId,
     SetStepGoalRequest request)
        {
            if (request.TargetSteps <= 0)
                throw new BadRequestException("Target steps must be greater than 0.");

            var today = GetToday();

            var goal = await _stepGoalRepository
                .GetTodayGoalAsync(currentUserId, today);

            if (goal == null)
            {
                goal = new StepGoal
                {
                    UserId = currentUserId,
                    EffectiveFrom = today,
                    TargetSteps = request.TargetSteps
                };

                await _repository.AddAsync(goal);
            }
            else
            {
                if (request.TargetSteps <= goal.TargetSteps)
                {
                    throw new BadRequestException("Target steps must be greater than the current target.");
                }
                else
                {
                    goal.TargetSteps = request.TargetSteps;

                    _repository.Update(goal);
                }
            }

            await _repository.SaveAsync();
        }

        public async Task<GoalProgressResponse> GetGoalProgressAsync(Guid currentUserId)
        {
            var today = GetToday();

            var goal = await _stepGoalRepository
                .GetCurrentGoalAsync(currentUserId, today);

            if (goal == null)
                throw new NotFoundException("Step goal not found.");

            var dailyStep = await _stepGoalRepository
                .GetDailyStepAsync(currentUserId, today);

            var currentSteps = dailyStep?.StepCount ?? 0;

            var percent = goal.TargetSteps == 0
                ? 0
                : (double)currentSteps / goal.TargetSteps * 100;

            if (percent > 100)
                percent = 100;

            return new GoalProgressResponse
            {
                TargetSteps = goal.TargetSteps,

                CurrentSteps = currentSteps,

                RemainingSteps = Math.Max(0,
                    goal.TargetSteps - currentSteps),

                ProgressPercent = Math.Round(percent, 2),

                Completed = currentSteps >= goal.TargetSteps
            };
        }

        public async Task<LongestStreakResponse> GetLongestStreakAsync(Guid currentUserId)
        {
            var history = await _stepGoalRepository
                .GetCompletedGoalHistoryAsync(currentUserId);

            if (!history.Any())
            {
                return new LongestStreakResponse
                {
                    LongestStreak = 0
                };
            }

            int longest = 1;
            int current = 1;

            for (int i = 1; i < history.Count; i++)
            {
                if (history[i].StepDate.DayNumber ==
                    history[i - 1].StepDate.DayNumber + 1)
                {
                    current++;
                }
                else
                {
                    longest = Math.Max(longest, current);
                    current = 1;
                }
            }

            longest = Math.Max(longest, current);

            return new LongestStreakResponse
            {
                LongestStreak = longest
            };
        }
        public async Task<CurrentStreakResponse> GetCurrentStreakAsync(Guid currentUserId)
        {
            var history = await _stepGoalRepository
                .GetCompletedGoalHistoryAsync(currentUserId);

            if (!history.Any())
                return new CurrentStreakResponse
                {
                    CurrentStreak = 0
                };

            var today = GetToday();

            int streak = 0;

            
            DateOnly expectedDate = history[0].StepDate == today
                ? today
                : today.AddDays(-1);

            foreach (var item in history)
            {
                if (item.StepDate == expectedDate)
                {
                    streak++;
                    expectedDate = expectedDate.AddDays(-1);
                }
                else if (item.StepDate < expectedDate)
                {
                    break;
                }
            }

            return new CurrentStreakResponse
            {
                CurrentStreak = streak
            };
        }
        private DateOnly GetToday()
        {
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

            var vnNow = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                vnTimeZone);

            return DateOnly.FromDateTime(vnNow);
        }
    }
}
