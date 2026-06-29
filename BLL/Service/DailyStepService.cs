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
    }
}
