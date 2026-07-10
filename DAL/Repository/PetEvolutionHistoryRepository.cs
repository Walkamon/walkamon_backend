using DAL.Data;
using DAL.Interfaces;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
namespace DAL.Repository
{
    public class PetEvolutionHistoryRepository
     : IPetEvolutionHistoryRepository
    {
        private readonly WalkamonContext _context;

        public PetEvolutionHistoryRepository(
            WalkamonContext context)
        {
            _context = context;
        }

        public async Task<List<PetEvolutionHistory>> GetHistoryAsync(Guid userId)
        {
            return await _context.PetEvolutionHistories
                .Include(x => x.Stage)
                .ThenInclude(x => x.Pet)
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.EvolvedAt)
                .ToListAsync();
        }

        public async Task<PetEvolutionHistory?> GetLatestAsync(Guid userId)
        {
            return await _context.PetEvolutionHistories
                .Include(x => x.Stage)
                .ThenInclude(x => x.Pet)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.EvolvedAt)
                .FirstOrDefaultAsync();
        }
    }
}
