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
    public class FeedbackRepository : IFeedbackRepository
    {
        private readonly WalkamonContext _context;

        public FeedbackRepository(WalkamonContext context)
        {
            _context = context;
        }

        public async Task<UserFeedback?> GetLatestFeedbackByUserIdAsync(Guid userId)
        {
            return await _context.UserFeedbacks
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }
    }
}
