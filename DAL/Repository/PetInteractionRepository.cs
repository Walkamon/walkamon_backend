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
    public class PetInteractionRepository : IPetInteractionRepository
    {
        private readonly WalkamonContext _context;

        public PetInteractionRepository(WalkamonContext context)
        {
            _context = context;
        }
        public async Task<PetInteraction?> GetTodayInteractionAsync(
    Guid userId,
    string type,
    DateOnly today)
        {
            return await _context.PetInteractions
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.InteractionType == type &&
                    x.InteractionDate == today);
        }
    }
}
