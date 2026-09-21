using BLL.Exceptions;
using BLL.Interfaces;
using DAL.Data;
using DAL.DTO;
using DAL.Extensions;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace BLL.Service
{
    public class StreakRewardService : IStreakRewardService
    {
        private readonly WalkamonContext _context;
        private readonly IStepGoalRepository _stepGoalRepository;
        public StreakRewardService(
            WalkamonContext context, IStepGoalRepository stepGoalRepository)
        {
            _stepGoalRepository = stepGoalRepository;
            _context = context;
        }
        public Task<ClaimRewardResponse> ClaimRewardAsync(Guid currentUserId)
        {
            return _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
            {
                var today = GetToday();
                var wallet = await _context.Wallets
                    .FromSqlInterpolated($"SELECT * FROM wallets WITH (UPDLOCK, HOLDLOCK) WHERE user_id = {currentUserId}")
                    .SingleOrDefaultAsync()
                    ?? throw new NotFoundException("Wallet not found.");
                var claimed = await _context.StreakRewardClaims
                    .AnyAsync(x => x.UserId == currentUserId && x.ClaimDate == today);

                if (claimed)
                    throw new BadRequestException("Reward has already been claimed today.");

                var goal = await _stepGoalRepository.GetCurrentGoalAsync(currentUserId, today);
                var steps = await _stepGoalRepository.GetDailyStepAsync(currentUserId, today);
                if (goal == null || goal.TargetSteps <= 0 || steps == null || steps.EligibleStepCount < goal.TargetSteps)
                    throw new BadRequestException("Complete today's walking goal before claiming the streak reward.");

                var history = await _stepGoalRepository.GetCompletedGoalHistoryAsync(currentUserId);
                var streak = 0;
                var expected = today;
                foreach (var day in history.Where(x => x.StepDate <= today).OrderByDescending(x => x.StepDate))
                {
                    if (day.StepDate != expected) break;
                    streak++;
                    expected = expected.AddDays(-1);
                }
                if (streak == 0)
                    throw new BadRequestException("No completed walking streak is available.");
                var reward = checked(streak * 10);
                wallet.Balance = checked(wallet.Balance + reward);
                _context.StreakRewardClaims.Add(new StreakRewardClaim
                {
                    UserId = currentUserId,
                    ClaimDate = today,
                    Reward = reward,
                    Streak = streak,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return new ClaimRewardResponse
                {
                    Streak = streak,
                    Reward = reward,
                    Balance = wallet.Balance,
                    ClaimDate = today
                };
            });
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
