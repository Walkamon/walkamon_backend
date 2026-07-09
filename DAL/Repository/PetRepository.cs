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
    }
}
