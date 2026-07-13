using DAL.Data;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repository
{
    public class PetRepository : IPetRepository
    {
        private readonly WalkamonContext _context;

        public PetRepository(WalkamonContext context)
        {
            _context = context;
        }

        public async Task<Pet?> GetStarterPetAsync()
        {
            return await _context.Pets
                .FirstOrDefaultAsync(x => x.PetName == "Starter");
        }
        public async Task<UserPet?> GetUserPetAsync(Guid userId)
        {
            return await _context.UserPets
                .FirstOrDefaultAsync(x => x.UserId == userId);
        }
        public async Task<Pet?> GetPetAsync(Guid petId)
        {
            return await _context.Pets
                .FirstOrDefaultAsync(x => x.PetId == petId);
        }
        public async Task<UserPet?> GetUserPetWithPetAsync(Guid userId)
        {
            return await _context.UserPets
                .Include(x => x.Pet)
                .FirstOrDefaultAsync(x => x.UserId == userId);
        }


        public async Task<List<Pet>> GetEvolutionOptionsAsync()
        {
            return await _context.Pets
                .Where(x => x.PetName != "Starter")
                .ToListAsync();
        }
        public async Task<List<PetStage>> GetStagesByPetIdAsync(Guid petId)
        {
            return await _context.PetStages
                .Where(x => x.PetId == petId)
                .OrderBy(x => x.StageNo)
                .ToListAsync();
        }
        public async Task<PetStage?> GetStageAsync(Guid petId, int stageNo)
        {
            return await _context.PetStages
                .FirstOrDefaultAsync(x =>
                    x.PetId == petId &&
                    x.StageNo == stageNo);
        }
        public async Task<PetStage?> GetFirstStageAsync(Guid petId)
        {
            return await _context.PetStages
                .Where(x => x.PetId == petId)
                .OrderBy(x => x.StageNo)
                .FirstOrDefaultAsync();
        }
        public async Task<List<PetAnimation>> GetAnimationsAsync(
    Guid petId,
    int stageNo)
        {
            return await _context.PetAnimations
                .Where(x =>
                    x.PetId == petId &&
                    x.PetStageUse == stageNo &&
                    x.IsActive)
                .ToListAsync();
        }
        public async Task<PetStage?> GetNextStageAsync(Guid petId, int currentStageNo)
        {
            return await _context.PetStages
                .FirstOrDefaultAsync(x =>
                    x.PetId == petId &&
                    x.StageNo == currentStageNo + 1);
        }
        public async Task<List<UserPet>> GetLeaderboardAsync()
        {
            return await _context.UserPets
                .Include(x => x.Pet)
                .Include(x => x.User)
                    .ThenInclude(x => x.UserProfile)
                .OrderByDescending(x => x.Level)
                .ThenByDescending(x => x.CurrentPetExp)
                .ToListAsync();
        }
        public async Task<UserPet?> GetFriendPetAsync(Guid userId)
        {
            return await _context.UserPets
                .Include(x => x.Pet)
                .Include(x => x.User)
                    .ThenInclude(x => x.UserProfile)
                .FirstOrDefaultAsync(x => x.UserId == userId);
        }
        public async Task<List<Pet>> GetEvolutionPetsAsync()
        {
            return await _context.Pets
                .Where(x => x.PetName != "Starter")
                .ToListAsync();
        }
    }
}
