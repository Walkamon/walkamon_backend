using DAL.Data;
using DAL.GenericRepository;
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
    public class ShopItemRepository : GenericRepository<ShopItem>, IShopItemRepository
    {
        private readonly WalkamonContext _context;

        public ShopItemRepository(WalkamonContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<List<ShopItem>> GetAllWithItemAsync()
        {
            return await _context.ShopItems
                .Include(x => x.Item)
                .ToListAsync();
        }
    }
}
