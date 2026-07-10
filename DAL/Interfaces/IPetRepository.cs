using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IPetRepository
    {
        Task<Pet?> GetStarterPetAsync();
        Task<UserPet?> GetUserPetAsync(Guid userId);
        Task<Pet?> GetPetAsync(Guid petId);
        Task<UserPet?> GetUserPetWithPetAsync(Guid userId);
        Task<List<Pet>> GetEvolutionOptionsAsync();

        Task<List<PetStage>> GetStagesByPetIdAsync(Guid petId);

        Task<PetStage?> GetStageAsync(Guid petId, int stageNo);

        Task<PetStage?> GetFirstStageAsync(Guid petId);
        Task<List<PetAnimation>> GetAnimationsAsync(
    Guid petId,
    int stageNo);
        Task<PetStage?> GetNextStageAsync(Guid petId, int currentStageNo);
        Task<List<UserPet>> GetLeaderboardAsync();
        Task<UserPet?> GetFriendPetAsync(Guid userId);
    }
}
