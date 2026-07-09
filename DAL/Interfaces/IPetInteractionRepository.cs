using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IPetInteractionRepository
    {
        Task<PetInteraction?> GetTodayInteractionAsync(
    Guid userId,
    string type,
    DateOnly today);
    }
}
