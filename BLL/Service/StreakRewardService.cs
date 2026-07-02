using BLL.Exceptions;
using BLL.Interfaces;
using DAL.Data;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using DAL.Repository;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Service
{
    public class StreakRewardService : IStreakRewardService
    {
        private readonly IStreakRewardRepository _claimRepository;
        private readonly IGenericRepository<Wallet> _walletRepository;
        private readonly IStepGoalRepository _stepGoalRepository;
        private readonly IGenericRepository<StreakRewardClaim> _streakRepository;
        public StreakRewardService(
    IStreakRewardRepository claimRepository,
    IGenericRepository<Wallet> walletRepository,
     IStepGoalRepository stepGoalRepository,
     IGenericRepository<StreakRewardClaim> streakRepository)
        {
            _stepGoalRepository = stepGoalRepository;
            _claimRepository = claimRepository;
            _walletRepository = walletRepository;
            _streakRepository = streakRepository;
        }
        public async Task<ClaimRewardResponse> ClaimRewardAsync(Guid currentUserId, CurrentStreakResponse currentStreak)
        {
            var today = GetToday();
            var claimed = await _claimRepository
          .HasClaimedTodayAsync(currentUserId, today);

            if (claimed)
                throw new BadRequestException("Reward has already been claimed today.");

            var reward = currentStreak.CurrentStreak * 10;

                var wallet = await _walletRepository.GetByIdAsync(currentUserId);
                    

                if (wallet == null)
                    throw new NotFoundException("Wallet not found.");

                wallet.Balance += reward;

                _walletRepository.Update(wallet);

                await _streakRepository.AddAsync(new StreakRewardClaim
                {
                    UserId = currentUserId,
                    ClaimDate = today,
                    Reward = reward,
                    Streak = currentStreak.CurrentStreak,
                    CreatedAt = DateTime.UtcNow
                });

                await _walletRepository.SaveAsync();

                

                return new ClaimRewardResponse
                {
                    Streak = currentStreak.CurrentStreak,
                    Reward = reward,
                    Balance = wallet.Balance,
                    ClaimDate = today
                };
            
            

        }

        private DateOnly GetToday()
        {
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

            var vnNow = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                vnTimeZone);

            return DateOnly.FromDateTime(vnNow);
        }
    }
}
