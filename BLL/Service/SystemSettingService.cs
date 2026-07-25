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
    public class SystemSettingService : ISystemSettingService
    {
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly IGenericRepository<SystemSetting> _repository;

        public SystemSettingService(
            ISystemSettingRepository systemSettingRepository,
            IGenericRepository<SystemSetting> repository)
        {
            _systemSettingRepository = systemSettingRepository;
            _repository = repository;
        }

        public async Task<StepExpRateResponse> GetStepExpRateAsync()
        {
            var setting = await _systemSettingRepository
                .GetByKeyAsync("StepToExpRate");

            if (setting == null)
                throw new NotFoundException("StepToExpRate not found.");

            return new StepExpRateResponse
            {
                SettingKey = setting.SettingKey,

                BaseExp = int.Parse(setting.SettingValue),

                Description =
                    "Every 100 validated daily steps awards the configured Pet EXP automatically."
            };
        }

        public async Task UpdateStepExpRateAsync(
            UpdateStepExpRateRequest request)
        {
            if (request.BaseExp <= 0)
                throw new BadRequestException(
                    "Base EXP must be greater than 0.");

            var setting = await _systemSettingRepository
                .GetByKeyAsync("StepToExpRate");

            if (setting == null)
                throw new NotFoundException(
                    "StepToExpRate not found.");

            setting.SettingValue = request.BaseExp.ToString();

            setting.UpdatedAt = DateTime.UtcNow;

            _repository.Update(setting);

            await _repository.SaveAsync();
        }
    }
}
