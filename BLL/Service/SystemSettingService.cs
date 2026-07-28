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
        public async Task<PetStatusSettingResponse> GetPetStatusSettingAsync()
        {
            var energy = await _systemSettingRepository
                .GetByKeyAsync("EnergyRecoverPerMinute");

            var bond = await _systemSettingRepository
                .GetByKeyAsync("BondDecreasePerMinute");

            var lifeForce = await _systemSettingRepository
                .GetByKeyAsync("LifeForceDecreasePerMinute");

            return new PetStatusSettingResponse
            {
                EnergyRecoverPerMinute = int.Parse(energy!.SettingValue),
                BondDecreasePerMinute = int.Parse(bond!.SettingValue),
                LifeForceDecreasePerMinute = int.Parse(lifeForce!.SettingValue)
            };
        }
        public async Task UpdatePetStatusSettingAsync(UpdatePetStatusSettingRequest request)
        {
            var energy = await _systemSettingRepository
                .GetByKeyAsync("EnergyRecoverPerMinute");

            var bond = await _systemSettingRepository
                .GetByKeyAsync("BondDecreasePerMinute");

            var lifeForce = await _systemSettingRepository
                .GetByKeyAsync("LifeForceDecreasePerMinute");

            if (energy == null || bond == null || lifeForce == null)
                throw new NotFoundException("Pet status settings not found.");

            energy.SettingValue = request.EnergyRecoverPerMinute.ToString();
            energy.UpdatedAt = DateTime.UtcNow;

            bond.SettingValue = request.BondDecreasePerMinute.ToString();
            bond.UpdatedAt = DateTime.UtcNow;

            lifeForce.SettingValue = request.LifeForceDecreasePerMinute.ToString();
            lifeForce.UpdatedAt = DateTime.UtcNow;

            _repository.Update(energy);
            _repository.Update(bond);
            _repository.Update(lifeForce);

            await _repository.SaveAsync();
        }
    }
}
