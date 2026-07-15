using BLL.Exceptions;
using BLL.Interfaces;
using BLL.Options;
using DAL.Data;
using DAL.DTO;
using DAL.Extensions;
using DAL.Interfaces;
using DAL.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;

namespace BLL.Service;

public class DailyLoginRewardService : IDailyLoginRewardService
{
    private const string VietnamTimeZoneId = "SE Asia Standard Time";

    private readonly IDailyLoginRewardRepository _repository;
    private readonly WalkamonContext _context;
    private readonly DailyLoginRewardOptions _options;

    public DailyLoginRewardService(
        IDailyLoginRewardRepository repository,
        WalkamonContext context,
        IOptions<DailyLoginRewardOptions> options)
    {
        _repository = repository;
        _context = context;
        _options = options.Value;
    }

    public async Task<DailyLoginRewardCalendarResponse> GetCalendarAsync(Guid userId)
    {
        var rewards = GetRewards();
        var today = GetToday();
        var lastClaim = await _repository.GetLatestClaimAsync(userId);

        return BuildCalendarResponse(today, lastClaim, rewards);
    }

    public async Task<DailyLoginRewardClaimResponse> ClaimAsync(Guid userId)
    {
        var rewards = GetRewards();
        var today = GetToday();

        try
        {
            return await _context.ExecuteInTransactionAsync(
                IsolationLevel.Serializable,
                async () =>
            {
                var lastClaim = await _repository.GetLatestClaimAsync(userId);
                if (lastClaim?.ClaimDate == today)
                {
                    throw new BadRequestException("Daily login reward has already been claimed today.");
                }

                var wallet = await _repository.GetWalletAsync(userId);
                if (wallet == null)
                {
                    throw new NotFoundException("Wallet not found.");
                }

                var claimedDay = GetNextDay(lastClaim, rewards.Length);
                var reward = rewards[claimedDay - 1];

                checked
                {
                    wallet.Balance += reward;
                }

                _repository.UpdateWallet(wallet);
                await _repository.AddClaimAsync(new DailyLoginRewardClaim
                {
                    UserId = userId,
                    ClaimDate = today,
                    CycleDay = claimedDay,
                    Reward = reward,
                    CreatedAt = DateTime.UtcNow
                });

                await _repository.SaveChangesAsync();

                return new DailyLoginRewardClaimResponse
                {
                    ClaimDate = today,
                    ClaimedDay = claimedDay,
                    Reward = reward,
                    Balance = wallet.Balance,
                    NextDay = claimedDay == rewards.Length ? 1 : claimedDay + 1
                };
            });
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new BadRequestException("Daily login reward has already been claimed today.");
        }
    }

    private static DailyLoginRewardCalendarResponse BuildCalendarResponse(
        DateOnly today,
        DailyLoginRewardClaim? lastClaim,
        IReadOnlyList<int> rewards)
    {
        var canClaimToday = lastClaim?.ClaimDate != today;
        var currentDay = GetNextDay(lastClaim, rewards.Count);
        var completedDays = lastClaim?.CycleDay == rewards.Count
            ? 0
            : lastClaim?.CycleDay ?? 0;

        return new DailyLoginRewardCalendarResponse
        {
            ServerDate = today,
            CanClaimToday = canClaimToday,
            LastClaimDate = lastClaim?.ClaimDate,
            CurrentDay = currentDay,
            Rewards = rewards.Select((reward, index) => new DailyLoginRewardCalendarItemResponse
            {
                Day = index + 1,
                Reward = reward,
                Status = index + 1 <= completedDays
                    ? "claimed"
                    : index + 1 == currentDay && canClaimToday
                        ? "claimable"
                        : "locked"
            }).ToList()
        };
    }

    private int[] GetRewards()
    {
        if (_options.Rewards is not { Length: 7 }
            || _options.Rewards.Any(reward => reward <= 0))
        {
            throw new InvalidOperationException(
                "DailyLoginReward:Rewards must contain exactly seven positive values.");
        }

        return _options.Rewards;
    }

    private static int GetNextDay(DailyLoginRewardClaim? lastClaim, int cycleLength)
    {
        if (lastClaim == null || lastClaim.CycleDay >= cycleLength)
        {
            return 1;
        }

        return lastClaim.CycleDay + 1;
    }

    private static DateOnly GetToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(VietnamTimeZoneId);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        return DateOnly.FromDateTime(now);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Number is 2601 or 2627;
    }
}
