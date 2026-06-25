using DAL.Interfaces;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DAL.Data;
namespace DAL.Repository
{
    public class DailyStepRepository : IDailyStepRepository
    {
        private readonly WalkamonContext _context;

        public DailyStepRepository(WalkamonContext context)
        {
            _context = context;
        }

        public async Task<DailyStep?> GetByUserAndDateAsync(
            Guid userId,
            DateOnly date)
        {
            return await _context.DailySteps
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.StepDate == date);
        }

       
    }
}
