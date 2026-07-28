using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BLL.Exceptions;
using BLL.Interfaces;
using BLL.Options;
using DAL.Data;
using DAL.DTO;
using DAL.Extensions;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BLL.Service;

public sealed partial class PvpSprintService : IPvpSprintService
{
    private const int InitialMmr = 1000;
    private const int RatingK = 32;
    private const int RatingDivisor = 400;
    private const int ReadyTimeoutSeconds = 30;
    private const int CountdownDeliveryLeadSeconds = 3;
    private const int CountdownDurationSeconds = 5;
    private const string DailyPowerScoringMode = "daily_power_v1";
    private static readonly string[] MatchTypes = ["ranked", "friendly", "event"];
    private static readonly string[] Results = ["win", "lose", "draw"];
    private readonly WalkamonContext _context;
    private readonly bool _realtimeEnabled;
    private readonly IValidatedStepService _validatedStepService;
    private readonly ILogger<PvpSprintService> _logger;
    private readonly TimePresentationSerializer _timePresentationSerializer;

    public PvpSprintService(
        WalkamonContext context,
        IOptions<PvpRealtimeOptions> realtimeOptions,
        IValidatedStepService validatedStepService,
        ILogger<PvpSprintService> logger,
        TimePresentationSerializer? timePresentationSerializer = null)
    {
        _context = context;
        _realtimeEnabled = realtimeOptions.Value.Enabled;
        _validatedStepService = validatedStepService;
        _logger = logger;
        _timePresentationSerializer = timePresentationSerializer
            ?? new TimePresentationSerializer(
                Microsoft.Extensions.Options.Options.Create(new TimePresentationOptions()));
    }

    public async Task<PvpInviteResponse> CreateInviteAsync(Guid userId, CreatePvpSprintInviteRequest request)
    {
        if (request.TargetUserId == Guid.Empty || request.TargetUserId == userId)
            throw new BadRequestException("You cannot invite yourself.");

        return await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
        var now = DateTime.UtcNow;
        await EnsureActiveUserAsync(userId);
        await EnsureActiveUserAsync(request.TargetUserId);
        await EnsureFriendshipAsync(userId, request.TargetUserId);
        await EnsureNoActivityAsync(userId);
        await EnsureNoActivityAsync(request.TargetUserId);

        var low = userId.CompareTo(request.TargetUserId) < 0 ? userId : request.TargetUserId;
        var high = userId.CompareTo(request.TargetUserId) < 0 ? request.TargetUserId : userId;
        var invite = new PvpSprintInvite
        {
            InviteId = Guid.NewGuid(), InviterUserId = userId, InviteeUserId = request.TargetUserId,
            UserLowId = low, UserHighId = high, StatusCode = "pending", CreatedAt = now, ExpiresAt = now.AddMinutes(1)
        };
        _context.PvpSprintInvites.Add(invite);
        AddActivity(userId, "invite_pending", invite.InviteId, invite.ExpiresAt, now);
        AddActivity(request.TargetUserId, "invite_pending", invite.InviteId, invite.ExpiresAt, now);
        AddOutbox("user", request.TargetUserId, "invite.created", new { inviteId = invite.InviteId, expiresAt = invite.ExpiresAt });
        await _context.SaveChangesAsync();
        return await ToInviteResponseAsync(invite, request.TargetUserId);
        });
    }

    public async Task<PvpInviteResponse> RespondInviteAsync(Guid userId, Guid inviteId, RespondPvpSprintInviteRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var responseUserId = await _context.ExecuteInTransactionAsync<Guid?>(IsolationLevel.Serializable, async () =>
        {
        var now = DateTime.UtcNow;
        var invite = await _context.PvpSprintInvites.FirstOrDefaultAsync(x => x.InviteId == inviteId)
            ?? throw new NotFoundException("Sprint invite not found.");
        if (invite.InviteeUserId != userId) throw new ForbiddenException("Only the invitee can respond.");
        if ((request.Accept && invite.StatusCode == "accepted") ||
            (!request.Accept && invite.StatusCode == "declined"))
            return invite.InviterUserId;
        if (invite.StatusCode != "pending") throw new ConflictException("Sprint invite is no longer pending.");
        if (invite.ExpiresAt <= now)
        {
            await ExpireInviteAsync(invite, now);
            await _context.SaveChangesAsync();
            return null;
        }

        invite.RespondedAt = now;
        if (!request.Accept)
        {
            invite.StatusCode = "declined";
            RemoveActivities(invite.InviteId);
            AddOutbox("user", invite.InviterUserId, "invite.declined", new { inviteId });
            await _context.SaveChangesAsync();
            return invite.InviterUserId;
        }

        var match = await CreateMatchAsync(invite.InviterUserId, userId, null, "friendly", "invite", now);
        invite.StatusCode = "accepted";
        invite.MatchId = match.MatchId;
        RemoveActivities(invite.InviteId);
        AddMatchActivity(invite.InviterUserId, match, now);
        AddMatchActivity(userId, match, now);
        AddMatchOutbox(match, "match.created");
        await _context.SaveChangesAsync();
        return invite.InviterUserId;
        });
        if (!responseUserId.HasValue)
            throw new ConflictException("Sprint invite has expired.");

        // Build the presentation response after committing so the Serializable
        // write transaction does not hold invite/activity locks during a
        // profile read.
        var savedInvite = await _context.PvpSprintInvites.AsNoTracking()
            .SingleAsync(x => x.InviteId == inviteId);
        var response = await ToInviteResponseAsync(savedInvite, responseUserId.Value);
        _logger.LogInformation(
            "PvP invite response completed. InviteId={InviteId} UserId={UserId} Accepted={Accepted} DurationMs={DurationMs}",
            inviteId,
            userId,
            request.Accept,
            stopwatch.ElapsedMilliseconds);
        return response;
    }

    public async Task CancelInviteAsync(Guid userId, Guid inviteId)
    {
        await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
        var invite = await _context.PvpSprintInvites.FirstOrDefaultAsync(x => x.InviteId == inviteId)
            ?? throw new NotFoundException("Sprint invite not found.");
        if (invite.InviterUserId != userId) throw new ForbiddenException("Only the inviter can cancel this invite.");
        if (invite.StatusCode != "pending") throw new ConflictException("Sprint invite is no longer pending.");
        invite.StatusCode = "cancelled";
        invite.RespondedAt = DateTime.UtcNow;
        RemoveActivities(invite.InviteId);
        AddOutbox("user", invite.InviteeUserId, "invite.cancelled", new { inviteId });
        await _context.SaveChangesAsync();
        });
    }

    public async Task<PvpPagedResponse<PvpInviteResponse>> GetInvitesAsync(Guid userId, string direction, string? status, int page, int pageSize)
    {
        ValidatePage(ref page, ref pageSize);
        if (!direction.Equals("incoming", StringComparison.OrdinalIgnoreCase) &&
            !direction.Equals("sent", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Direction must be incoming or sent.");
        var allowedStatuses = new[] { "pending", "accepted", "declined", "cancelled", "expired" };
        if (!string.IsNullOrWhiteSpace(status) && !allowedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("Invite status is invalid.");
        var query = _context.PvpSprintInvites.AsNoTracking().AsQueryable();
        query = direction.Equals("sent", StringComparison.OrdinalIgnoreCase)
            ? query.Where(x => x.InviterUserId == userId)
            : query.Where(x => x.InviteeUserId == userId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.ToLowerInvariant();
            query = query.Where(x => x.StatusCode == normalizedStatus);
        }
        var total = await query.CountAsync();
        var invites = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var responses = new List<PvpInviteResponse>();
        foreach (var invite in invites)
            responses.Add(await ToInviteResponseAsync(invite, invite.InviterUserId == userId ? invite.InviteeUserId : invite.InviterUserId));
        return new PvpPagedResponse<PvpInviteResponse> { Page = page, PageSize = pageSize, Total = total, Items = responses };
    }

    public async Task<PvpMatchmakingStatusResponse> JoinMatchmakingAsync(Guid userId, JoinPvpMatchmakingRequest request)
    {
        if (!string.Equals(request.MatchTypeCode, "ranked", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Only ranked matchmaking is available.");
        return await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
        var now = DateTime.UtcNow;
        await EnsureActiveUserAsync(userId);
        await EnsureNoActivityAsync(userId);
        var profile = await EnsureProfileAsync(userId, now);
        await EnsureRewardMatrixAsync("ranked");
        var waiting = await _context.MatchmakingQueues.Where(x => x.StatusCode == "waiting" && x.UserId != userId)
            .OrderBy(x => x.QueuedAt).ToListAsync();
        MatchmakingQueue? candidate = null;
        foreach (var queue in waiting)
        {
            var candidateProfile = await EnsureProfileAsync(queue.UserId, now);
            if (Math.Abs(candidateProfile.Mmr - profile.Mmr) <= 100 && await IsActiveUserAsync(queue.UserId)) { candidate = queue; break; }
        }
        if (candidate == null)
        {
            _context.MatchmakingQueues.Add(new MatchmakingQueue { UserId = userId, MatchTypeCode = "ranked", StatusCode = "waiting", QueuedAt = now });
            AddActivity(userId, "queue_waiting", userId, now.AddSeconds(15), now);
            AddOutbox("user", userId, "queue.waiting", new { queuedAt = now });
            await _context.SaveChangesAsync();
            return new PvpMatchmakingStatusResponse
            {
                ActivityType = "queue_waiting",
                StatusCode = "waiting",
                QueuedAt = now,
                BotFallbackAt = now.AddSeconds(15),
                ServerTime = now
            };
        }

        _context.MatchmakingQueues.Remove(candidate);
        RemoveActivity(candidate.UserId);
        var match = await CreateMatchAsync(userId, candidate.UserId, null, "ranked", "matchmaking", now);
        AddMatchActivity(userId, match, now);
        AddMatchActivity(candidate.UserId, match, now);
        AddMatchOutbox(match, "match.created");
        await _context.SaveChangesAsync();
        return new PvpMatchmakingStatusResponse
        {
            ActivityType = "match_countdown",
            StatusCode = "countdown",
            MatchId = match.MatchId,
            ServerTime = now
        };
        });
    }

    public async Task<PvpMatchmakingStatusResponse> GetMatchmakingStatusAsync(Guid userId)
    {
        await EnsureActiveUserAsync(userId);
        var now = DateTime.UtcNow;
        var activity = await _context.PvpPlayerActivities.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId);
        if (activity == null)
            return new PvpMatchmakingStatusResponse { ServerTime = now };

        if (activity.ActivityType == "queue_waiting")
        {
            var queue = await _context.MatchmakingQueues.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.StatusCode == "waiting");
            if (queue == null)
                return new PvpMatchmakingStatusResponse { ServerTime = now };
            return new PvpMatchmakingStatusResponse
            {
                ActivityType = activity.ActivityType,
                StatusCode = "waiting",
                QueuedAt = AsUtc(queue.QueuedAt),
                BotFallbackAt = AsUtc(queue.QueuedAt.AddSeconds(15)),
                ServerTime = now
            };
        }

        if (activity.ActivityType.StartsWith("match_", StringComparison.Ordinal))
        {
            var match = await _context.PvpMatches.AsNoTracking()
                .FirstOrDefaultAsync(x => x.MatchId == activity.ActivityId);
            if (match == null || match.StatusCode is "finished" or "cancelled")
                return new PvpMatchmakingStatusResponse { ServerTime = now };
            return new PvpMatchmakingStatusResponse
            {
                ActivityType = activity.ActivityType,
                StatusCode = match.StatusCode,
                MatchId = match.MatchId,
                CountdownStartsAt = GetCountdownStartsAt(match.CountdownEndsAt),
                CountdownEndsAt = AsUtc(match.CountdownEndsAt),
                ServerTime = now
            };
        }

        return new PvpMatchmakingStatusResponse
        {
            ActivityType = activity.ActivityType,
            StatusCode = activity.ActivityType,
            ServerTime = now
        };
    }

    public async Task CancelMatchmakingAsync(Guid userId)
    {
        await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
        var queue = await _context.MatchmakingQueues.FirstOrDefaultAsync(x => x.UserId == userId && x.StatusCode == "waiting")
            ?? throw new NotFoundException("No waiting matchmaking queue found.");
        _context.MatchmakingQueues.Remove(queue);
        RemoveActivity(userId);
        await _context.SaveChangesAsync();
        });
    }

    public Task<PvpMatchResponse> GetMatchAsync(Guid userId, Guid matchId) => BuildMatchResponseAsync(matchId, userId);

    public async Task<PvpResultResponse> ForfeitMatchAsync(Guid userId, Guid matchId)
    {
        await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
            var now = DateTime.UtcNow;
            var match = await GetMatchForUpdateAsync(matchId)
                ?? throw new NotFoundException("Sprint match not found.");
            var quitter = match.PvpMatchPlayers.SingleOrDefault(x => x.UserId == userId)
                ?? throw new ForbiddenException("You are not a participant in this sprint match.");

            if (match.StatusCode == "finished" &&
                match.FinishReasonCode == "user_forfeit" &&
                match.ForfeitedByUserId == userId)
            {
                return;
            }

            if (match.StatusCode is not ("countdown" or "running"))
                throw new ConflictException("Sprint match can no longer be forfeited.");

            var players = match.PvpMatchPlayers.OrderBy(x => x.JoinedAt).ToList();
            if (players.Count != 2)
                throw new ConflictException("Sprint match does not have exactly two participants.");

            var opponent = players.Single(x => x.MatchPlayerId != quitter.MatchPlayerId);
            quitter.ResultCode = "lose";
            opponent.ResultCode = "win";
            match.WinnerUserId = opponent.UserId;

            if (match.MatchTypeCode == "ranked")
            {
                var first = players[0];
                var second = players[1];
                var firstDelta = PvpRatingCalculator.CalculateDelta(
                    first.MmrBefore,
                    second.MmrBefore,
                    first.ResultCode!,
                    match.RatingK,
                    match.RatingDivisor);
                first.MmrDelta = firstDelta;
                second.MmrDelta = -firstDelta;

                foreach (var player in players.Where(x => x.UserId.HasValue))
                {
                    var profile = await EnsureProfileAsync(player.UserId!.Value, now);
                    profile.Mmr += player.MmrDelta;
                    profile.UpdatedAt = now;
                }
            }
            else
            {
                foreach (var player in players)
                    player.MmrDelta = 0;
            }

            if (opponent.UserId.HasValue)
            {
                var rewardSnapshots = await _context.PvpMatchRewardSnapshots
                    .Include(x => x.Items)
                    .Where(x => x.MatchId == match.MatchId)
                    .ToListAsync();
                var winSnapshot = rewardSnapshots.SingleOrDefault(x => x.ResultCode == "win")
                    ?? throw new ConflictException("Sprint win reward snapshot is missing.");
                await CreateEntitlementAsync(match, opponent, [winSnapshot], now);
            }

            foreach (var session in await _context.PvpStepSessions
                         .Where(x => x.MatchId == match.MatchId && x.StatusCode == "active")
                         .ToListAsync())
            {
                session.StatusCode = "closed";
                session.ClosedReason = "user_forfeit";
            }

            foreach (var effect in await _context.PvpMatchEffects
                         .Where(x => x.MatchId == match.MatchId && x.StatusCode == "active")
                         .ToListAsync())
            {
                effect.StatusCode = "expired";
                effect.EndsAt = now;
            }

            foreach (var player in players.Where(x => x.UserId.HasValue))
                RemoveActivity(player.UserId!.Value);

            match.StatusCode = "finished";
            match.FinishReasonCode = "user_forfeit";
            match.ForfeitedByUserId = userId;
            match.EndedAt = now;
            match.SettlementEndsAt = now;
            match.ResolvedAt = now;

            var eventDetails = new
            {
                finishReasonCode = match.FinishReasonCode,
                forfeitedByUserId = userId,
                winnerUserId = match.WinnerUserId
            };
            AddMatchOutbox(match, "match.forfeited", notifyUsers: true, details: eventDetails);
            AddMatchOutbox(match, "match.finished", notifyUsers: true, details: eventDetails);
            await _context.SaveChangesAsync();
        });

        return await GetResultAsync(userId, matchId);
    }

    public Task<PvpMatchReadyResponse> ReadyMatchAsync(Guid userId, Guid matchId) =>
        _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
            var now = DateTime.UtcNow;
            var match = await GetMatchForUpdateAsync(matchId)
                ?? throw new NotFoundException("Sprint match not found.");
            var actor = match.PvpMatchPlayers.SingleOrDefault(x => x.UserId == userId)
                ?? throw new ForbiddenException("You are not a participant in this sprint match.");

            if (match.StatusCode == "cancelled")
                throw new ConflictException("Sprint match has been cancelled.");

            if (match.StatusCode != "countdown" || match.CountdownEndsAt.HasValue)
                return ToReadyResponse(match, now);

            actor.IsReady = true;
            var allReady = match.PvpMatchPlayers.Count == 2 && match.PvpMatchPlayers.All(x => x.IsReady);
            if (allReady)
            {
                var countdownStartsAt = CeilingToSecond(now).AddSeconds(CountdownDeliveryLeadSeconds);
                match.CountdownEndsAt = countdownStartsAt.AddSeconds(CountdownDurationSeconds);
                foreach (var activity in await _context.PvpPlayerActivities
                             .Where(x => x.ActivityId == match.MatchId)
                             .ToListAsync())
                {
                    activity.DueAt = match.CountdownEndsAt;
                    activity.UpdatedAt = now;
                }

                AddMatchOutbox(
                    match,
                    "match.countdown.started",
                    notifyUsers: true,
                    details: new
                    {
                        countdownStartsAt = AsUtc(countdownStartsAt),
                        countdownEndsAt = AsUtc(match.CountdownEndsAt)
                    });
            }

            await _context.SaveChangesAsync();
            return ToReadyResponse(match, now);
        });

    public async Task<PvpResultResponse> GetResultAsync(Guid userId, Guid matchId)
    {
        var match = await GetMatchForUserAsync(matchId, userId);
        if (match.StatusCode != "finished") throw new ConflictException("Sprint result is not available yet.");
        var response = await BuildMatchResponseAsync(matchId, userId);
        var participant = match.PvpMatchPlayers.Single(x => x.UserId == userId);
        var entitlement = await _context.PvpMatchRewardEntitlements.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MatchId == matchId && x.UserId == userId);
        var tiers = await _context.PvpRankTiers.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        var rankBefore = PvpGameplayCalculator.ResolveTier(participant.MmrBefore, tiers);
        var rankAfter = PvpGameplayCalculator.ResolveTier(participant.MmrBefore + participant.MmrDelta, tiers);
        return new PvpResultResponse
        {
            MatchId = response.MatchId, MatchTypeCode = response.MatchTypeCode, SourceCode = response.SourceCode, StatusCode = response.StatusCode,
            FinishReasonCode = response.FinishReasonCode, ForfeitedByUserId = response.ForfeitedByUserId,
            WinnerUserId = response.WinnerUserId, ResolvedAt = response.ResolvedAt,
            CreatedAt = response.CreatedAt, CountdownStartsAt = response.CountdownStartsAt,
            CountdownEndsAt = response.CountdownEndsAt, StartedAt = response.StartedAt,
            EndedAt = response.EndedAt, SettlementEndsAt = response.SettlementEndsAt, Participants = response.Participants,
            ServerTime = response.ServerTime, RuleVersion = response.RuleVersion, LastEventSequence = response.LastEventSequence,
            ActiveEffects = response.ActiveEffects, Loadout = response.Loadout,
            MmrBefore = participant.MmrBefore, MmrDelta = participant.MmrDelta, MmrAfter = participant.MmrBefore + participant.MmrDelta,
            RankBefore = ToTierResponse(rankBefore), RankAfter = ToTierResponse(rankAfter), TierChanged = rankBefore.TierCode != rankAfter.TierCode,
            CanClaimReward = entitlement is { ClaimedAt: null }, ClaimedAt = AsUtc(entitlement?.ClaimedAt)
        };
    }

    public async Task<PvpPagedResponse<PvpMatchResponse>> GetHistoryAsync(Guid userId, int page, int pageSize, string? matchType, string? result, DateTime? from, DateTime? to, bool includeActive)
    {
        ValidatePage(ref page, ref pageSize);
        if (!string.IsNullOrWhiteSpace(matchType) && !MatchTypes.Contains(matchType, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("Match type is invalid.");
        if (!string.IsNullOrWhiteSpace(result) && !Results.Contains(result, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("Result is invalid.");
        var normalizedResult = string.IsNullOrWhiteSpace(result) ? null : result.ToLowerInvariant();
        var query = _context.PvpMatches.AsNoTracking().Where(x => x.PvpMatchPlayers.Any(p => p.UserId == userId && (normalizedResult == null || p.ResultCode == normalizedResult)));
        if (!includeActive) query = query.Where(x => x.StatusCode == "finished" || x.StatusCode == "cancelled");
        if (!string.IsNullOrWhiteSpace(matchType))
        {
            var normalizedMatchType = matchType.ToLowerInvariant();
            query = query.Where(x => x.MatchTypeCode == normalizedMatchType);
        }
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from);
        if (to.HasValue) query = query.Where(x => x.CreatedAt <= to);
        var total = await query.CountAsync();
        var ids = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.MatchId).ToListAsync();
        var items = new List<PvpMatchResponse>();
        foreach (var id in ids) items.Add(await BuildMatchResponseAsync(id, userId));
        return new PvpPagedResponse<PvpMatchResponse> { Page = page, PageSize = pageSize, Total = total, Items = items };
    }

    public async Task<PvpStepSessionResponse> CreateStepSessionAsync(Guid userId, Guid matchId, CreatePvpStepSessionRequest request)
        => await _validatedStepService.CreatePvpSessionAsync(userId, matchId, request);

    public async Task<PvpStepBatchResponse> SubmitStepBatchAsync(Guid userId, Guid matchId, Guid sessionId, SubmitPvpStepBatchRequest request)
        => await _validatedStepService.SubmitPvpBatchAsync(userId, matchId, sessionId, request);

    public async Task<PvpRewardClaimResponse> ClaimRewardAsync(Guid userId, Guid matchId)
    {
        return await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
        var entitlement = await _context.PvpMatchRewardEntitlements.Include(x => x.Items).FirstOrDefaultAsync(x => x.MatchId == matchId && x.UserId == userId)
            ?? throw new NotFoundException("Sprint reward entitlement not found.");
        if (entitlement.ClaimedAt != null) throw new ConflictException("Sprint reward has already been claimed.");
        var wallet = await _context.Wallets.FirstOrDefaultAsync(x => x.UserId == userId) ?? throw new NotFoundException("Wallet not found.");
        checked { wallet.Balance += entitlement.WalletAmount; }
        foreach (var reward in entitlement.Items)
        {
            var inventory = await _context.InventoryItems.FirstOrDefaultAsync(x => x.UserId == userId && x.ItemId == reward.ItemId);
            if (inventory == null) _context.InventoryItems.Add(new InventoryItem { UserId = userId, ItemId = reward.ItemId, Quantity = reward.Quantity });
            else checked { inventory.Quantity += reward.Quantity; }
        }
        entitlement.ClaimedAt = DateTime.UtcNow;
        AddOutbox("user", userId, "reward.claimed", new { matchId, entitlement.MatchRewardEntitlementId });
        await _context.SaveChangesAsync();
        return new PvpRewardClaimResponse { WalletBalance = wallet.Balance, WalletReward = entitlement.WalletAmount, RewardItems = entitlement.Items.Select(x => new PvpRewardItemRequest { ItemId = x.ItemId, Quantity = x.Quantity }).ToList() };
        });
    }

    public async Task<List<PvpRewardRuleResponse>> GetRewardRulesAsync() =>
        await _context.PvpRewardRules.AsNoTracking()
            .Include(x => x.RewardPackage)
            .ThenInclude(x => x.RewardPackageItems)
            .OrderBy(x => x.MatchTypeCode)
            .ThenBy(x => x.ResultCode)
            .Select(x => new PvpRewardRuleResponse
            {
                MatchTypeCode = x.MatchTypeCode,
                ResultCode = x.ResultCode,
                WalletAmount = x.RewardPackage.WalletAmount,
                IsActive = x.IsActive,
                RewardItems = x.RewardPackage.RewardPackageItems
                    .OrderBy(item => item.ItemId)
                    .Select(item => new PvpRewardItemRequest { ItemId = item.ItemId, Quantity = item.Quantity })
                    .ToList()
            })
            .ToListAsync();

    public async Task UpdateRewardRulesAsync(UpdatePvpRewardRulesRequest request)
    {
        var expected = MatchTypes.SelectMany(type => Results.Select(result => $"{type}:{result}")).Order().ToArray();
        var received = request.Rules.Select(x => $"{x.MatchTypeCode}:{x.ResultCode}").Order().ToArray();
        if (request.Rules.Count != 9 || !expected.SequenceEqual(received)) throw new BadRequestException("Exactly nine ranked, friendly and event reward rules are required.");
        if (request.Rules.Any(x => x.WalletAmount < 0 || (x.WalletAmount == 0 && x.RewardItems.Count == 0) || x.RewardItems.Any(i => i.Quantity <= 0 || i.ItemId == Guid.Empty))) throw new BadRequestException("Reward rule values are invalid.");
        await _context.ExecuteInTransactionAsync(IsolationLevel.ReadCommitted, async () =>
        {
        foreach (var ruleRequest in request.Rules)
        {
            var existing = await _context.PvpRewardRules.Include(x => x.RewardPackage).FirstOrDefaultAsync(x => x.MatchTypeCode == ruleRequest.MatchTypeCode && x.ResultCode == ruleRequest.ResultCode);
            RewardPackage package;
            if (existing == null)
            {
                package = new RewardPackage { RewardPackageId = Guid.NewGuid(), PackageName = $"pvp-{ruleRequest.MatchTypeCode}-{ruleRequest.ResultCode}-{Guid.NewGuid():N}", WalletAmount = ruleRequest.WalletAmount };
                existing = new PvpRewardRule { PvpRewardRuleId = Guid.NewGuid(), MatchTypeCode = ruleRequest.MatchTypeCode, ResultCode = ruleRequest.ResultCode, RewardPackageId = package.RewardPackageId, RewardPackage = package, IsActive = true, UpdatedAt = DateTime.UtcNow };
                _context.PvpRewardRules.Add(existing);
            }
            else { package = existing.RewardPackage; package.WalletAmount = ruleRequest.WalletAmount; existing.IsActive = true; existing.UpdatedAt = DateTime.UtcNow; }
            var oldItems = await _context.RewardPackageItems.Where(x => x.RewardPackageId == package.RewardPackageId).ToListAsync();
            _context.RewardPackageItems.RemoveRange(oldItems);
            foreach (var item in ruleRequest.RewardItems.GroupBy(x => x.ItemId).Select(g => new PvpRewardItemRequest { ItemId = g.Key, Quantity = g.Sum(x => x.Quantity) }))
            {
                if (!await _context.Items.AnyAsync(x => x.ItemId == item.ItemId && x.IsActive)) throw new BadRequestException("A reward item does not exist or is inactive.");
                _context.RewardPackageItems.Add(new RewardPackageItem { RewardPackageId = package.RewardPackageId, ItemId = item.ItemId, Quantity = item.Quantity });
            }
        }
        await _context.SaveChangesAsync();
        });
    }

    public async Task ProcessDueWorkAsync(CancellationToken cancellationToken = default)
    {
        const int batchSize = 100;
        var now = DateTime.UtcNow;

        var inviteIds = await _context.PvpSprintInvites.AsNoTracking()
            .Where(x => x.StatusCode == "pending" && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)
            .Select(x => x.InviteId)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var inviteId in inviteIds)
            await ProcessLifecycleRecordAsync("invite", inviteId, () => ProcessExpiredInviteAsync(inviteId, cancellationToken));

        var queueUserIds = await _context.MatchmakingQueues.AsNoTracking()
            .Where(x => x.StatusCode == "waiting" && x.QueuedAt <= now.AddSeconds(-15))
            .OrderBy(x => x.QueuedAt)
            .Select(x => x.UserId)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var queueUserId in queueUserIds)
            await ProcessLifecycleRecordAsync("queue", queueUserId, () => ProcessDueQueueAsync(queueUserId, cancellationToken));

        var readyTimeoutIds = await _context.PvpPlayerActivities.AsNoTracking()
            .Where(x => x.ActivityType == "match_countdown" && x.DueAt <= now)
            .Join(
                _context.PvpMatches.AsNoTracking().Where(x => x.StatusCode == "countdown" && x.CountdownEndsAt == null),
                activity => activity.ActivityId,
                match => match.MatchId,
                (_, match) => match.MatchId)
            .Distinct()
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var matchId in readyTimeoutIds)
            await ProcessLifecycleRecordAsync("ready-timeout", matchId, () => ProcessReadyTimeoutAsync(matchId, cancellationToken));

        var countdownIds = await _context.PvpMatches.AsNoTracking()
            .Where(x => x.StatusCode == "countdown" && x.CountdownEndsAt != null && x.CountdownEndsAt <= now)
            .OrderBy(x => x.CountdownEndsAt)
            .Select(x => x.MatchId)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var matchId in countdownIds)
            await ProcessLifecycleRecordAsync("countdown", matchId, () => ProcessDueCountdownAsync(matchId, cancellationToken));

        if (_realtimeEnabled)
        {
            var botMatchIds = await _context.PvpMatches.AsNoTracking()
                .Where(x => x.StatusCode == "running" && x.StartedAt != null && x.EndedAt > now &&
                            x.PvpMatchPlayers.Any(p => p.ParticipantTypeCode == "bot"))
                .OrderBy(x => x.StartedAt)
                .Select(x => x.MatchId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            foreach (var matchId in botMatchIds)
                await ProcessLifecycleRecordAsync("bot-items", matchId, () => ProcessBotItemActionsForMatchAsync(matchId, cancellationToken));

            var effectMatchIds = await _context.PvpMatchEffects.AsNoTracking()
                .Where(x => x.StatusCode == "active" && x.EndsAt <= now)
                .OrderBy(x => x.EndsAt)
                .Select(x => x.MatchId)
                .Distinct()
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            foreach (var matchId in effectMatchIds)
                await ProcessLifecycleRecordAsync("effect-expiry", matchId, () => ProcessEffectExpirationsForMatchAsync(matchId, cancellationToken));
        }

        now = DateTime.UtcNow;
        var progressIds = await _context.PvpMatches.AsNoTracking()
            .Where(x => x.StatusCode == "running" &&
                        x.ScoringModeCode == DailyPowerScoringMode &&
                        x.StartedAt != null &&
                        x.EndedAt > now)
            .OrderBy(x => x.StartedAt)
            .Select(x => x.MatchId)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var matchId in progressIds)
            await ProcessLifecycleRecordAsync("progress", matchId, () => ProcessRunningProgressAsync(matchId, cancellationToken));

        now = DateTime.UtcNow;
        var runningIds = await _context.PvpMatches.AsNoTracking()
            .Where(x => x.StatusCode == "running" && x.EndedAt <= now)
            .OrderBy(x => x.EndedAt)
            .Select(x => x.MatchId)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var matchId in runningIds)
            await ProcessLifecycleRecordAsync("running", matchId, () => ProcessDueRunningAsync(matchId, cancellationToken));

        var settlingIds = await _context.PvpMatches.AsNoTracking()
            .Where(x => x.StatusCode == "settling" && x.SettlementEndsAt <= now)
            .OrderBy(x => x.SettlementEndsAt)
            .Select(x => x.MatchId)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var matchId in settlingIds)
            await ProcessLifecycleRecordAsync("settlement", matchId, () => ProcessDueSettlementAsync(matchId, cancellationToken));
    }

    private async Task ProcessLifecycleRecordAsync(string operation, Guid aggregateId, Func<Task> action)
    {
        try
        {
            _context.ChangeTracker.Clear();
            await action();
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogDebug("PvP {Operation} {AggregateId} was already handled by another worker.", operation, aggregateId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PvP {Operation} failed for {AggregateId}; remaining records will continue.", operation, aggregateId);
        }
        finally
        {
            _context.ChangeTracker.Clear();
        }
    }

    private Task ProcessExpiredInviteAsync(Guid inviteId, CancellationToken cancellationToken) =>
        _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
            var now = DateTime.UtcNow;
            var invite = await _context.PvpSprintInvites
                .FirstOrDefaultAsync(x => x.InviteId == inviteId && x.StatusCode == "pending" && x.ExpiresAt <= now, cancellationToken);
            if (invite == null) return;
            await ExpireInviteAsync(invite, now);
            await _context.SaveChangesAsync(cancellationToken);
        });

    private Task ProcessDueQueueAsync(Guid userId, CancellationToken cancellationToken) =>
        _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
            var now = DateTime.UtcNow;
            var queue = await _context.MatchmakingQueues
                .FirstOrDefaultAsync(x => x.UserId == userId && x.StatusCode == "waiting" && x.QueuedAt <= now.AddSeconds(-15), cancellationToken);
            if (queue == null) return;
            if (!await IsActiveUserAsync(queue.UserId))
            {
                _context.MatchmakingQueues.Remove(queue);
                RemoveActivity(queue.UserId);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            var playerProfile = await EnsureProfileAsync(queue.UserId, now);
            var bot = await _context.PvpBotProfiles
                .Where(x => x.IsActive)
                .OrderBy(x => Math.Abs(x.Mmr - playerProfile.Mmr))
                .ThenBy(x => x.BotProfileId)
                .FirstOrDefaultAsync(cancellationToken);
            if (bot == null) return;

            _context.MatchmakingQueues.Remove(queue);
            RemoveActivity(queue.UserId);
            var match = await CreateMatchAsync(queue.UserId, null, bot, "ranked", "bot", now);
            AddMatchActivity(queue.UserId, match, now);
            AddMatchOutbox(match, "match.created");
            await _context.SaveChangesAsync(cancellationToken);
        });

    private Task ProcessDueCountdownAsync(Guid matchId, CancellationToken cancellationToken) =>
        _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
            var now = DateTime.UtcNow;
            var match = await _context.PvpMatches
                .Include(x => x.PvpMatchPlayers)
                .FirstOrDefaultAsync(x => x.MatchId == matchId && x.StatusCode == "countdown" &&
                                          x.CountdownEndsAt != null && x.CountdownEndsAt <= now, cancellationToken);
            if (match == null) return;
            if (match.CountdownEndsAt!.Value < now.AddMinutes(-2))
            {
                await CancelMatchAsync(match, "lifecycle_timeout", now, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }
            if (match.PvpMatchPlayers.Count != 2 || match.PvpMatchPlayers.Any(x => !x.IsReady))
            {
                await CancelMatchAsync(match, "player_not_ready", now, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            match.StatusCode = "running";
            match.StartedAt = now;
            match.EndedAt = now.AddSeconds(30);
            if (match.ScoringModeCode == DailyPowerScoringMode)
                await SnapshotDailyPowerAsync(match, now, cancellationToken);
            if (_realtimeEnabled) await ApplySpiritPassivesAsync(match, now, cancellationToken);
            foreach (var activity in await _context.PvpPlayerActivities
                         .Where(x => x.ActivityId == match.MatchId)
                         .ToListAsync(cancellationToken))
            {
                activity.ActivityType = "match_running";
                activity.DueAt = match.EndedAt;
                activity.UpdatedAt = now;
            }
            AddMatchOutbox(match, "match.started");
            await _context.SaveChangesAsync(cancellationToken);
        });

    private Task ProcessReadyTimeoutAsync(Guid matchId, CancellationToken cancellationToken) =>
        _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
            var now = DateTime.UtcNow;
            var match = await _context.PvpMatches
                .Include(x => x.PvpMatchPlayers)
                .FirstOrDefaultAsync(x => x.MatchId == matchId &&
                                          x.StatusCode == "countdown" &&
                                          x.CountdownEndsAt == null, cancellationToken);
            if (match == null) return;
            var dueAt = await _context.PvpPlayerActivities
                .Where(x => x.ActivityId == matchId && x.ActivityType == "match_countdown")
                .MinAsync(x => x.DueAt, cancellationToken);
            if (!dueAt.HasValue || dueAt > now) return;

            await CancelMatchAsync(match, "ready_timeout", now, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        });

    private Task ProcessDueRunningAsync(Guid matchId, CancellationToken cancellationToken) =>
        _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
            var now = DateTime.UtcNow;
            var match = await _context.PvpMatches
                .Include(x => x.PvpMatchPlayers)
                .FirstOrDefaultAsync(x => x.MatchId == matchId && x.StatusCode == "running" && x.EndedAt <= now, cancellationToken);
            if (match == null) return;
            if (match.ScoringModeCode == DailyPowerScoringMode)
            {
                await RecalculateDailyPowerDistancesAsync(
                    match,
                    match.EndedAt!.Value,
                    cancellationToken);
                AddProgressOutbox(match, match.EndedAt.Value);
            }
            match.StatusCode = "settling";
            // Daily-power scoring is entirely server-authoritative, so there is
            // no late sensor batch to wait for. Keep the settling state/event
            // for client compatibility, then allow settlement immediately.
            match.SettlementEndsAt = now;
            foreach (var activity in await _context.PvpPlayerActivities
                         .Where(x => x.ActivityId == match.MatchId)
                         .ToListAsync(cancellationToken))
            {
                activity.ActivityType = "match_settling";
                activity.DueAt = match.SettlementEndsAt;
                activity.UpdatedAt = now;
            }
            AddMatchOutbox(match, "match.settling");
            await _context.SaveChangesAsync(cancellationToken);
        });

    private Task ProcessDueSettlementAsync(Guid matchId, CancellationToken cancellationToken) =>
        _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
            var now = DateTime.UtcNow;
            var match = await _context.PvpMatches
                .Include(x => x.PvpMatchPlayers)
                .FirstOrDefaultAsync(x => x.MatchId == matchId && x.StatusCode == "settling" && x.SettlementEndsAt <= now, cancellationToken);
            if (match == null) return;
            await ResolveMatchAsync(match, now, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        });

    private async Task CancelMatchAsync(PvpMatch match, string reason, DateTime now, CancellationToken cancellationToken)
    {
        match.StatusCode = "cancelled";
        match.CancelReason = reason;
        match.ResolvedAt = now;
        foreach (var player in match.PvpMatchPlayers.Where(x => x.UserId.HasValue))
            RemoveActivity(player.UserId!.Value);
        foreach (var session in await _context.PvpStepSessions
                     .Where(x => x.MatchId == match.MatchId && x.StatusCode == "active")
                     .ToListAsync(cancellationToken))
        {
            session.StatusCode = "closed";
            session.ClosedReason = reason;
        }
        AddMatchOutbox(match, "match.cancelled", notifyUsers: true);
    }

    private async Task ResolveMatchAsync(PvpMatch match, DateTime now, CancellationToken cancellationToken)
    {
        var players = match.PvpMatchPlayers.OrderBy(x => x.JoinedAt).ToList();
        if (players.Count != 2)
        {
            await CancelMatchAsync(match, "invalid_participant_count", now, cancellationToken);
            return;
        }
        var rewardSnapshots = await _context.PvpMatchRewardSnapshots
            .Include(x => x.Items)
            .Where(x => x.MatchId == match.MatchId)
            .ToListAsync(cancellationToken);
        if (rewardSnapshots.Count != 3 ||
            !Results.All(result => rewardSnapshots.Any(snapshot => snapshot.ResultCode == result)))
        {
            await CancelMatchAsync(match, "reward_snapshot_missing", now, cancellationToken);
            return;
        }
        if (match.ScoringModeCode == DailyPowerScoringMode && match.EndedAt.HasValue)
            await RecalculateDailyPowerDistancesAsync(match, match.EndedAt.Value, cancellationToken);
        else
        {
            var bot = players.SingleOrDefault(x => x.ParticipantTypeCode == "bot");
            if (bot?.BotProfileId != null)
                await CalculateBotDistanceAsync(match, bot, cancellationToken);
        }
        var first = players[0]; var second = players[1];
        if (first.DistanceUnits == second.DistanceUnits) { first.ResultCode = "draw"; second.ResultCode = "draw"; }
        else if (first.DistanceUnits > second.DistanceUnits) { first.ResultCode = "win"; second.ResultCode = "lose"; }
        else { first.ResultCode = "lose"; second.ResultCode = "win"; }
        var winner = players.SingleOrDefault(x => x.ResultCode == "win");
        match.WinnerUserId = winner?.UserId;
        if (match.MatchTypeCode == "ranked")
        {
            var delta = PvpRatingCalculator.CalculateDelta(first.MmrBefore, second.MmrBefore, first.ResultCode!, match.RatingK, match.RatingDivisor);
            first.MmrDelta = delta; second.MmrDelta = -delta;
            foreach (var player in players.Where(x => x.UserId.HasValue))
            {
                var profile = await EnsureProfileAsync(player.UserId!.Value, now);
                profile.Mmr += player.MmrDelta;
                profile.UpdatedAt = now;
            }
        }
        foreach (var player in players.Where(x => x.UserId.HasValue))
            await CreateEntitlementAsync(match, player, rewardSnapshots, now);
        match.StatusCode = "finished";
        match.FinishReasonCode = "normal_completion";
        match.ForfeitedByUserId = null;
        match.ResolvedAt = now;
        foreach (var player in players.Where(x => x.UserId.HasValue)) RemoveActivity(player.UserId!.Value);
        foreach (var session in await _context.PvpStepSessions
                     .Where(x => x.MatchId == match.MatchId && x.StatusCode == "active")
                     .ToListAsync(cancellationToken))
        {
            session.StatusCode = "closed";
            session.ClosedReason = "match_finished";
        }
        AddMatchOutbox(match, "match.finished", notifyUsers: true);
    }

    private Task CreateEntitlementAsync(
        PvpMatch match,
        PvpMatchPlayer player,
        IReadOnlyCollection<PvpMatchRewardSnapshot> snapshots,
        DateTime now)
    {
        var snapshot = snapshots.Single(x => x.ResultCode == player.ResultCode);
        var entitlement = new PvpMatchRewardEntitlement { MatchRewardEntitlementId = Guid.NewGuid(), MatchId = match.MatchId, UserId = player.UserId!.Value, ResultCode = player.ResultCode!, WalletAmount = snapshot.WalletAmount, CreatedAt = now };
        _context.PvpMatchRewardEntitlements.Add(entitlement);
        foreach (var item in snapshot.Items)
            entitlement.Items.Add(new PvpMatchRewardItem { MatchRewardEntitlementId = entitlement.MatchRewardEntitlementId, ItemId = item.ItemId, Quantity = item.Quantity });
        return Task.CompletedTask;
    }

    private async Task<PvpMatch> CreateMatchAsync(Guid firstUserId, Guid? secondUserId, PvpBotProfile? bot, string matchType, string source, DateTime now)
    {
        var firstProfile = await EnsureProfileAsync(firstUserId, now);
        var match = new PvpMatch
        {
            MatchId = Guid.NewGuid(),
            MatchTypeCode = matchType,
            SourceCode = source,
            StatusCode = "countdown",
            CreatedAt = now,
            CountdownEndsAt = null,
            RatingK = RatingK,
            RatingDivisor = RatingDivisor,
            SpeedMinBps = 7500,
            SpeedMaxBps = 12500,
            ItemSlotLimit = 2,
            RuleVersion = 2,
            ScoringModeCode = DailyPowerScoringMode,
            DailyStepPowerCap = PvpGameplayCalculator.DefaultDailyStepPowerCap,
            BasePaceMinMilliStepsPerSecond = PvpGameplayCalculator.DefaultMinimumPaceMilliStepsPerSecond,
            BasePaceMaxMilliStepsPerSecond = PvpGameplayCalculator.DefaultMaximumPaceMilliStepsPerSecond
        };
        _context.PvpMatches.Add(match);
        await SnapshotMatchRewardsAsync(match, now);
        var firstPet = await GetPetSnapshotAsync(firstUserId);
        var firstPlayer = new PvpMatchPlayer { MatchPlayerId = Guid.NewGuid(), MatchId = match.MatchId, UserId = firstUserId, ParticipantTypeCode = "user", StepsAtMatch = 0, PetLevelAtMatch = firstPet.Level, PetIdSnapshot = firstPet.PetId, PetNameSnapshot = firstPet.Name, PetStageNoSnapshot = firstPet.StageNo, SpiritAffinityCode = firstPet.AffinityCode, Score = 0, MmrBefore = firstProfile.Mmr, BasePaceMilliStepsPerSecond = match.BasePaceMinMilliStepsPerSecond, IsReady = false, JoinedAt = now };
        _context.PvpMatchPlayers.Add(firstPlayer);
        await SnapshotPlayerLoadoutAsync(match, firstPlayer, firstUserId, now);
        if (secondUserId.HasValue)
        {
            var secondProfile = await EnsureProfileAsync(secondUserId.Value, now);
            var secondPet = await GetPetSnapshotAsync(secondUserId.Value);
            var secondPlayer = new PvpMatchPlayer { MatchPlayerId = Guid.NewGuid(), MatchId = match.MatchId, UserId = secondUserId, ParticipantTypeCode = "user", StepsAtMatch = 0, PetLevelAtMatch = secondPet.Level, PetIdSnapshot = secondPet.PetId, PetNameSnapshot = secondPet.Name, PetStageNoSnapshot = secondPet.StageNo, SpiritAffinityCode = secondPet.AffinityCode, Score = 0, MmrBefore = secondProfile.Mmr, BasePaceMilliStepsPerSecond = match.BasePaceMinMilliStepsPerSecond, IsReady = false, JoinedAt = now };
            _context.PvpMatchPlayers.Add(secondPlayer);
            await SnapshotPlayerLoadoutAsync(match, secondPlayer, secondUserId.Value, now);
        }
        else if (bot != null)
        {
            var botAffinity = NormalizeAffinityCode(bot.SpiritAffinityCode);
            var botPaceMilli = checked((int)Math.Round(
                bot.StepsPerSecond * 1000m,
                MidpointRounding.AwayFromZero));
            var botPlayer = new PvpMatchPlayer { MatchPlayerId = Guid.NewGuid(), MatchId = match.MatchId, BotProfileId = bot.BotProfileId, ParticipantTypeCode = "bot", SpiritAffinityCode = botAffinity, PetStageNoSnapshot = NormalizePetStageNo(botAffinity, bot.PetStageNo), StepsAtMatch = 0, PetLevelAtMatch = 1, Score = 0, MmrBefore = bot.Mmr, BasePaceMilliStepsPerSecond = botPaceMilli, IsReady = true, JoinedAt = now };
            _context.PvpMatchPlayers.Add(botPlayer);
            await SnapshotBotLoadoutAsync(match, botPlayer, bot.BotProfileId, now);
        }
        AddMatchAssigned(firstUserId, match, now);
        if (secondUserId.HasValue) AddMatchAssigned(secondUserId.Value, match, now);
        return match;
    }

    private async Task SnapshotMatchRewardsAsync(PvpMatch match, DateTime now)
    {
        var rules = await _context.PvpRewardRules.AsNoTracking()
            .Include(x => x.RewardPackage)
            .ThenInclude(x => x.RewardPackageItems)
            .Where(x => x.MatchTypeCode == match.MatchTypeCode && x.IsActive)
            .ToListAsync();
        if (rules.Count != 3 || !Results.All(result => rules.Any(rule => rule.ResultCode == result)))
            throw new ConflictException("Sprint reward configuration is incomplete.");

        foreach (var rule in rules)
        {
            var snapshot = new PvpMatchRewardSnapshot
            {
                MatchRewardSnapshotId = Guid.NewGuid(),
                MatchId = match.MatchId,
                ResultCode = rule.ResultCode,
                WalletAmount = rule.RewardPackage.WalletAmount,
                CreatedAt = now
            };
            foreach (var item in rule.RewardPackage.RewardPackageItems)
                snapshot.Items.Add(new PvpMatchRewardSnapshotItem
                {
                    MatchRewardSnapshotId = snapshot.MatchRewardSnapshotId,
                    ItemId = item.ItemId,
                    Quantity = item.Quantity
                });
            _context.PvpMatchRewardSnapshots.Add(snapshot);
        }
    }

    private async Task<PvpMatch> GetMatchForUserAsync(Guid matchId, Guid userId)
    {
        var match = await _context.PvpMatches.Include(x => x.PvpMatchPlayers).ThenInclude(x => x.User).ThenInclude(x => x!.UserProfile).Include(x => x.PvpMatchPlayers).ThenInclude(x => x.BotProfile).FirstOrDefaultAsync(x => x.MatchId == matchId)
            ?? throw new NotFoundException("Sprint match not found.");
        if (!match.PvpMatchPlayers.Any(x => x.UserId == userId)) throw new ForbiddenException("You are not a participant in this sprint match.");
        return match;
    }

    private Task<PvpMatch?> GetMatchForUpdateAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _context.PvpMatches
            .FromSqlInterpolated($"""
                SELECT *
                FROM dbo.pvp_matches WITH (UPDLOCK, HOLDLOCK)
                WHERE match_id = {matchId}
                """)
            .Include(x => x.PvpMatchPlayers)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<PvpMatchResponse> BuildMatchResponseAsync(Guid matchId, Guid userId)
    {
        var match = await GetMatchForUserAsync(matchId, userId);
        var now = DateTime.UtcNow;
        var actor = match.PvpMatchPlayers.Single(x => x.UserId == userId);
        var activeEffects = await _context.PvpMatchEffects.AsNoTracking()
            .Where(x => x.MatchId == matchId && x.StatusCode == "active" && x.EndsAt > now)
            .OrderBy(x => x.EndsAt).ToListAsync();
        var loadout = await _context.PvpMatchLoadoutSlots.AsNoTracking().Where(x => x.MatchPlayerId == actor.MatchPlayerId)
            .Join(_context.Items, slot => slot.ItemId, item => item.ItemId, (slot, item) => new PvpMatchLoadoutSlotResponse
            {
                MatchLoadoutSlotId = slot.PvpMatchLoadoutSlotId, SlotNo = slot.SlotNo, ItemId = slot.ItemId,
                ItemName = item.ItemName, EffectCode = slot.EffectCode, AssetKey = slot.AssetKey, UsedAt = slot.UsedAt
            }).OrderBy(x => x.SlotNo).ToListAsync();
        foreach (var slot in loadout)
        {
            slot.UsedAt = AsUtc(slot.UsedAt);
            slot.Quantity = await _context.InventoryItems.AsNoTracking().Where(x => x.UserId == userId && x.ItemId == slot.ItemId).Select(x => (int?)x.Quantity).FirstOrDefaultAsync() ?? 0;
        }
        return new PvpMatchResponse
        {
            MatchId = match.MatchId, MatchTypeCode = match.MatchTypeCode, SourceCode = match.SourceCode,
            StatusCode = match.StatusCode, FinishReasonCode = match.FinishReasonCode,
            ForfeitedByUserId = match.ForfeitedByUserId, WinnerUserId = match.WinnerUserId,
            CreatedAt = AsUtc(match.CreatedAt),
            CountdownStartsAt = GetCountdownStartsAt(match.CountdownEndsAt),
            CountdownEndsAt = AsUtc(match.CountdownEndsAt), StartedAt = AsUtc(match.StartedAt), EndedAt = AsUtc(match.EndedAt), SettlementEndsAt = AsUtc(match.SettlementEndsAt), ResolvedAt = AsUtc(match.ResolvedAt),
            ServerTime = now, RuleVersion = match.RuleVersion, ScoringModeCode = match.ScoringModeCode,
            DailyStepPowerCap = match.DailyStepPowerCap, LastEventSequence = match.LastEventSequence,
            ActiveEffects = activeEffects.Select(ToEffectResponse).ToList(), Loadout = loadout,
            Participants = match.PvpMatchPlayers.Select(x =>
            {
                var effects = activeEffects.Where(e => e.TargetMatchPlayerId == x.MatchPlayerId && e.EffectKindCode is "buff" or "debuff").Select(e => (e.EffectKindCode, e.MagnitudeBps));
                var affinityCode = NormalizeAffinityCode(x.SpiritAffinityCode);
                var stageNo = NormalizePetStageNo(affinityCode, x.PetStageNoSnapshot);
                return new PvpParticipantResponse { MatchPlayerId = x.MatchPlayerId, ParticipantTypeCode = x.ParticipantTypeCode, UserId = x.UserId, BotProfileId = x.BotProfileId, DisplayName = x.User?.UserProfile?.Username ?? x.BotProfile?.DisplayName ?? "Player", AvatarUrl = x.User?.UserProfile?.AvatarUrl ?? x.BotProfile?.AvatarUrl, PetId = x.PetIdSnapshot, PetName = x.PetNameSnapshot, PetLevel = x.PetLevelAtMatch, PetStageNo = stageNo, PetVisualCode = $"{affinityCode}_stage{stageNo}", Score = x.Score, ValidatedSteps = x.ValidatedSteps, DailyEligibleStepsSnapshot = x.DailyEligibleStepsSnapshot, BasePaceMilliStepsPerSecond = x.BasePaceMilliStepsPerSecond, DistanceUnits = x.DistanceUnits, SpiritAffinityCode = affinityCode, PassiveSpeedBps = x.PassiveSpeedBps, SpeedMultiplierBps = PvpGameplayCalculator.CalculateSpeedBps(x.PassiveSpeedBps, effects, match.SpeedMinBps, match.SpeedMaxBps), IsReady = x.IsReady, ResultCode = x.ResultCode };
            }).ToList()
        };
    }

    private async Task<PvpInviteResponse> ToInviteResponseAsync(PvpSprintInvite invite, Guid otherUserId)
    {
        var user = await _context.Users.Include(x => x.UserProfile).AsNoTracking().FirstAsync(x => x.UserId == otherUserId);
        return new PvpInviteResponse { InviteId = invite.InviteId, User = new PvpUserSummaryResponse { UserId = user.UserId, Username = user.UserProfile?.Username, AvatarUrl = user.UserProfile?.AvatarUrl }, StatusCode = invite.StatusCode, ExpiresAt = invite.ExpiresAt, CreatedAt = invite.CreatedAt, MatchId = invite.MatchId };
    }

    private async Task EnsureActiveUserAsync(Guid userId)
    {
        if (!await IsActiveUserAsync(userId)) throw new ForbiddenException("User is unavailable for Lumina Sprint.");
    }
    private Task<bool> IsActiveUserAsync(Guid userId) => _context.Users.AnyAsync(x => x.UserId == userId && x.StatusCode == "active" && x.DeletedAt == null);
    private async Task EnsureFriendshipAsync(Guid first, Guid second)
    {
        var low = first.CompareTo(second) < 0 ? first : second; var high = first.CompareTo(second) < 0 ? second : first;
        if (!await _context.Friendships.AnyAsync(x => x.UserLowId == low && x.UserHighId == high)) throw new ForbiddenException("Sprint invite is only available for friends.");
    }
    private async Task EnsureNoActivityAsync(Guid userId)
    {
        if (await _context.PvpPlayerActivities.AnyAsync(x => x.UserId == userId)) throw new ConflictException("Player already has an active PvP activity.");
    }
    private async Task EnsureRewardMatrixAsync(string matchType)
    {
        if (await _context.PvpRewardRules.CountAsync(x => x.MatchTypeCode == matchType && x.IsActive) != 3) throw new ConflictException("Sprint reward configuration is incomplete.");
    }
    private async Task<PvpPlayerProfile> EnsureProfileAsync(Guid userId, DateTime now)
    {
        var tracked = _context.PvpPlayerProfiles.Local.FirstOrDefault(x => x.UserId == userId);
        if (tracked != null) return tracked;
        var profile = await _context.PvpPlayerProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile != null) return profile;
        profile = new PvpPlayerProfile { UserId = userId, Mmr = InitialMmr, UpdatedAt = now };
        _context.PvpPlayerProfiles.Add(profile);
        return profile;
    }
    private async Task<byte> GetPetLevelAsync(Guid userId) => (await _context.UserPets.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId))?.Level is int level and > 0 ? (byte)Math.Min(level, byte.MaxValue) : (byte)1;
    private void AddActivity(Guid userId, string type, Guid activityId, DateTime? dueAt, DateTime now) => _context.PvpPlayerActivities.Add(new PvpPlayerActivity { UserId = userId, ActivityType = type, ActivityId = activityId, DueAt = dueAt, CreatedAt = now, UpdatedAt = now });
    private void AddMatchActivity(Guid userId, PvpMatch match, DateTime now)
    {
        var dueAt = match.CountdownEndsAt ?? now.AddSeconds(ReadyTimeoutSeconds);
        var existing = _context.PvpPlayerActivities.Local.FirstOrDefault(x => x.UserId == userId);
        if (existing != null)
        {
            existing.ActivityType = "match_countdown";
            existing.ActivityId = match.MatchId;
            existing.DueAt = dueAt;
            existing.UpdatedAt = now;
            if (_context.Entry(existing).State == EntityState.Deleted) _context.Entry(existing).State = EntityState.Modified;
            return;
        }
        AddActivity(userId, "match_countdown", match.MatchId, dueAt, now);
    }
    private void RemoveActivity(Guid userId)
    {
        var tracked = _context.PvpPlayerActivities.Local.FirstOrDefault(x => x.UserId == userId) ?? _context.PvpPlayerActivities.FirstOrDefault(x => x.UserId == userId);
        if (tracked != null) _context.PvpPlayerActivities.Remove(tracked);
    }
    private void RemoveActivities(Guid activityId)
    {
        var activities = _context.PvpPlayerActivities.Where(x => x.ActivityId == activityId).ToList();
        _context.PvpPlayerActivities.RemoveRange(activities);
    }
    private Task ExpireInviteAsync(PvpSprintInvite invite, DateTime now)
    {
        invite.StatusCode = "expired"; invite.RespondedAt = now; RemoveActivities(invite.InviteId); AddOutbox("user", invite.InviterUserId, "invite.expired", new { inviteId = invite.InviteId }); return Task.CompletedTask;
    }
    private void AddOutbox(string aggregateType, Guid aggregateId, string eventType, object payload) => _context.OutboxEvents.Add(new OutboxEvent { EventId = Guid.NewGuid(), AggregateType = aggregateType, AggregateId = aggregateId, Destination = "signalr", EventType = eventType, PayloadJson = _timePresentationSerializer.Serialize(payload), CreatedAt = DateTime.UtcNow });
    private void AddMatchAssigned(Guid userId, PvpMatch match, DateTime now) =>
        AddOutbox("user", userId, "match.assigned", new
        {
            matchId = match.MatchId,
            matchTypeCode = match.MatchTypeCode,
            sourceCode = match.SourceCode,
            statusCode = match.StatusCode,
            countdownStartsAt = GetCountdownStartsAt(match.CountdownEndsAt),
            countdownEndsAt = AsUtc(match.CountdownEndsAt),
            readyExpiresAt = AsUtc(now.AddSeconds(ReadyTimeoutSeconds)),
            lastEventSequence = match.LastEventSequence,
            serverTime = now
        });
    private void AddMatchOutbox(PvpMatch match, string eventType, bool notifyUsers = false, object? details = null)
    {
        var now = DateTime.UtcNow;
        var sequence = ++match.LastEventSequence;
        var payload = _timePresentationSerializer.Serialize(new { matchId = match.MatchId, statusCode = match.StatusCode, sequence, serverTime = now, details = details ?? new { } });
        _context.PvpMatchEvents.Add(new PvpMatchEvent { PvpMatchEventId = Guid.NewGuid(), MatchId = match.MatchId, Sequence = sequence, EventType = eventType, PayloadJson = payload, CreatedAt = now });
        _context.OutboxEvents.Add(new OutboxEvent { EventId = Guid.NewGuid(), AggregateType = "match", AggregateId = match.MatchId, Destination = "signalr", EventType = eventType, PayloadJson = payload, CreatedAt = now });
        if (!notifyUsers) return;
        foreach (var userId in match.PvpMatchPlayers.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct())
            _context.OutboxEvents.Add(new OutboxEvent { EventId = Guid.NewGuid(), AggregateType = "user", AggregateId = userId, Destination = "signalr", EventType = eventType, PayloadJson = payload, CreatedAt = now });
    }
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static DateTime? AsUtc(DateTime? value) => value.HasValue ? AsUtc(value.Value) : null;
    private static DateTime CeilingToSecond(DateTime value)
    {
        var utc = AsUtc(value);
        var remainder = utc.Ticks % TimeSpan.TicksPerSecond;
        return remainder == 0 ? utc : utc.AddTicks(TimeSpan.TicksPerSecond - remainder);
    }
    private static DateTime? GetCountdownStartsAt(DateTime? countdownEndsAt) =>
        countdownEndsAt.HasValue
            ? AsUtc(countdownEndsAt.Value).AddSeconds(-CountdownDurationSeconds)
            : null;
    private static PvpMatchReadyResponse ToReadyResponse(PvpMatch match, DateTime serverTime) =>
        new()
        {
            MatchId = match.MatchId,
            StatusCode = match.StatusCode,
            AllReady = match.PvpMatchPlayers.Count == 2 && match.PvpMatchPlayers.All(x => x.IsReady),
            CountdownStartsAt = GetCountdownStartsAt(match.CountdownEndsAt),
            CountdownEndsAt = AsUtc(match.CountdownEndsAt),
            LastEventSequence = match.LastEventSequence,
            ServerTime = AsUtc(serverTime)
        };
    private static void ValidatePage(ref int page, ref int pageSize) { page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100); }
}
