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
    public class PetService : IPetService
    {
        private readonly IPetRepository _petRepository;
        private readonly IGenericRepository<UserPet> _repository;

        public PetService(
           IPetRepository petRepository, IGenericRepository<UserPet> repository    )
        {
            _petRepository = petRepository;
            _repository = repository;
        }
        public async Task CreateUserPetAsync(Guid userId, CreateUserPetRequest request)
        {
            var starterPet = await _petRepository.GetStarterPetAsync();

            if (starterPet == null)
                throw new NotFoundException("Starter pet not found.");

            var userPet = new UserPet
            {
                UserId = userId,
                PetId = starterPet.PetId,

                PetName = request.PetName,

                Level = 1,

                PetExp = starterPet.Exp,
                PetEnergy = starterPet.Energy,
                PetBond = starterPet.Bond,
                PetLifeForce = starterPet.LifeForce,

                CurrentPetExp = starterPet.Exp,
                CurrentPetEnergy = starterPet.Energy,
                CurrentPetBond = starterPet.Bond,
                CurrentPetLifeForce = starterPet.LifeForce
            };
            var existed = await _repository.AnyAsync(x => x.UserId == userId);

            if (existed)
                throw new BadRequestException("User already has a pet.");
            await _repository.AddAsync(userPet);
            await _repository.SaveAsync();
        }
        public async Task<PetStatusResponse> GetPetStatusAsync(Guid currentUserId)
        {
            var pet = await _petRepository.GetUserPetAsync(currentUserId);

            if (pet == null)
                throw new NotFoundException("Pet not found.");

            UpdateEnergy(pet);

            UpdateBond(pet);

            UpdateLifeForce(pet);

            _repository.Update(pet);

            await _repository.SaveAsync();

            return new PetStatusResponse
            {
                CurrentEnergy = pet.CurrentPetEnergy,
                MaxEnergy = pet.PetEnergy,

                CurrentBond = pet.CurrentPetBond,
                MaxBond = pet.PetBond,

                CurrentLifeForce = pet.CurrentPetLifeForce,
                MaxLifeForce = pet.PetLifeForce
            };
        }

        private void UpdateEnergy(UserPet pet)
        {
            var now = GetVietnamNow();

            var elapsedMinutes =
                (int)(now - pet.EnergyUpdatedAt).TotalMinutes;

            if (elapsedMinutes <= 0)
                return;

            pet.CurrentPetEnergy = Math.Min(
                pet.PetEnergy,
                pet.CurrentPetEnergy + elapsedMinutes);

            pet.EnergyUpdatedAt =
                pet.EnergyUpdatedAt.AddMinutes(elapsedMinutes);
        }

        private void UpdateBond(UserPet pet)
        {
            var now = GetVietnamNow();

            var elapsedMinutes =
                (int)(now - pet.BondUpdatedAt).TotalMinutes;

            int cycles = elapsedMinutes / 20;

            if (cycles <= 0)
                return;

            pet.CurrentPetBond = Math.Max(
                0,
                pet.CurrentPetBond - cycles * 10);

            pet.BondUpdatedAt =
                pet.BondUpdatedAt.AddMinutes(cycles * 20);
        }

        private void UpdateLifeForce(UserPet pet)
        {
            var now = GetVietnamNow();

            var elapsedMinutes =
                (int)(now - pet.LifeForceUpdatedAt).TotalMinutes;

            int cycles = elapsedMinutes / 20;

            if (cycles <= 0)
                return;

            pet.CurrentPetLifeForce = Math.Max(
                0,
                pet.CurrentPetLifeForce - cycles * 10);

            pet.LifeForceUpdatedAt =
                pet.LifeForceUpdatedAt.AddMinutes(cycles * 20);
        }

        private static DateTime GetVietnamNow()
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        }
    }
}

