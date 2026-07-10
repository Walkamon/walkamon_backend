using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IPetEvolutionHistoryRepository
    {
        Task<List<PetEvolutionHistory>> GetHistoryAsync(Guid userId);

        Task<PetEvolutionHistory?> GetLatestAsync(Guid userId);
    }
}
