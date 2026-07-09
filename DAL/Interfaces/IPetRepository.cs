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
    }
}
