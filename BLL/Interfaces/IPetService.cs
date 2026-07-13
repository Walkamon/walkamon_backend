using DAL.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IPetService
    {
         Task CreateUserPetAsync(Guid userId, CreateUserPetRequest request);
        Task<PetStatusResponse> GetPetStatusAsync(Guid currentUserId);
        Task<LevelPetResponse> AddPetExpAsync(
    Guid currentUserId,
    int exp);

        Task<PetStatusResponse> FeedSpiritAsync(Guid userId);
        Task<PetStatusResponse> TapSpiritAsync(Guid userId);
        Task<PetInfoResponse> GetPetInfoAsync(Guid userId);
        Task<List<EvolutionOptionResponse>> GetEvolutionOptionsAsync(Guid userId);

        Task EvolveStarterAsync(Guid userId, Guid petId);
        Task<List<EvolutionStageResponse>> GetEvolutionStagesAsync(Guid userId);
        Task<EvolutionStageResponse> EvolveNextAsync(Guid userId);
        Task<List<PetLeaderboardResponse>> GetLeaderboardAsync();
        Task<List<EvolutionHistoryResponse>> GetEvolutionHistoryAsync(Guid userId);
        Task<FriendSpiritResponse> GetFriendSpiritAsync(Guid friendUserId);
        Task<List<PetEvolutionPreviewResponse>>
GetEvolutionPreviewAsync();
    }
}
