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
    }
}
