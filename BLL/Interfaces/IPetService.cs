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
    }
}
