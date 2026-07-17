using System.Data;
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
using Microsoft.Extensions.Options;

namespace BLL.Service;

public sealed partial class PvpSprintService : IPvpSprintService
{
    private const int InitialMmr = 1000;
    private const int RatingK = 32;
    private const int RatingDivisor = 400;
    private static readonly string[] MatchTypes = ["ranked", "friendly", "event"];
    private static readonly string[] Results = ["win", "lose", "draw"];
    private readonly WalkamonContext _context;
    private readonly bool _realtimeEnabled;
    private readonly IValidatedStepService _validatedStepService;

    public PvpSprintService(
        WalkamonContext context,
        IOptions<PvpRealtimeOptions> realtimeOptions,
        IValidatedStepService validatedStepService)
    {
        _context = context;
        _realtimeEnabled = realtimeOptions.Value.Enabled;
        _validatedStepService = validatedStepService;
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
        var response = await _context.ExecuteInTransactionAsync<PvpInviteResponse?>(IsolationLevel.Serializable, async () =>
        {
        var now = DateTime.UtcNow;
        var invite = await _context.PvpSprintInvites.FirstOrDefaultAsync(x => x.InviteId == inviteId)
            ?? throw new NotFoundException("Sprint invite not found.");
        if (invite.InviteeUserId != userId) throw new ForbiddenException("Only the invitee can respond.");
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
            return await ToInviteResponseAsync(invite, invite.InviterUserId);
        }

        var match = await CreateMatchAsync(invite.InviterUserId, userId, null, "friendly", "invite", now);
        invite.StatusCode = "accepted";
        invite.MatchId = match.MatchId;
        RemoveActivities(invite.InviteId);
        AddMatchActivity(invite.InviterUserId, match, now);
        AddMatchActivity(userId, match, now);
        AddMatchOutbox(match, "match.created");
        await _context.SaveChangesAsync();
        return await ToInviteResponseAsync(invite, invite.InviterUserId);
        });
        return response ?? throw new ConflictException("Sprint invite has expired.");
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
        var query = _context.PvpSprintInvites.AsNoTracking().AsQueryable();
        query = direction.Equals("sent", StringComparison.OrdinalIgnoreCase)
            ? query.Where(x => x.InviterUserId == userId)
            : query.Where(x => x.InviteeUserId == userId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.StatusCode == status);
        var total = await query.CountAsync();
        var invites = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var responses = new List<PvpInviteResponse>();
        foreach (var invite in invites)
            responses.Add(await ToInviteResponseAsync(invite, invite.InviterUserId == userId ? invite.InviteeUserId : invite.InviterUserId));
        return new PvpPagedResponse<PvpInviteResponse> { Page = page, PageSize = pageSize, Total = total, Items = responses };
    }

    public async Task<PvpMatchResponse> JoinMatchmakingAsync(Guid userId, JoinPvpMatchmakingRequest request)
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
            return new PvpMatchResponse { MatchId = Guid.Empty, MatchTypeCode = "ranked", StatusCode = "waiting", CreatedAt = now };
        }

        _context.MatchmakingQueues.Remove(candidate);
        RemoveActivity(candidate.UserId);
        var match = await CreateMatchAsync(userId, candidate.UserId, null, "ranked", "matchmaking", now);
        AddMatchActivity(userId, match, now);
        AddMatchActivity(candidate.UserId, match, now);
        AddMatchOutbox(match, "match.created");
        await _context.SaveChangesAsync();
        return await BuildMatchResponseAsync(match.MatchId, userId);
        });
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
            MatchId = response.MatchId, MatchTypeCode = response.MatchTypeCode, StatusCode = response.StatusCode,
            CreatedAt = response.CreatedAt, CountdownEndsAt = response.CountdownEndsAt, StartedAt = response.StartedAt,
            EndedAt = response.EndedAt, SettlementEndsAt = response.SettlementEndsAt, Participants = response.Participants,
            ServerTime = response.ServerTime, RuleVersion = response.RuleVersion, ActiveEffects = response.ActiveEffects, Loadout = response.Loadout,
            MmrBefore = participant.MmrBefore, MmrDelta = participant.MmrDelta, MmrAfter = participant.MmrBefore + participant.MmrDelta,
            RankBefore = ToTierResponse(rankBefore), RankAfter = ToTierResponse(rankAfter), TierChanged = rankBefore.TierCode != rankAfter.TierCode,
            CanClaimReward = entitlement is { ClaimedAt: null }, ClaimedAt = entitlement?.ClaimedAt
        };
    }

    public async Task<PvpPagedResponse<PvpMatchResponse>> GetHistoryAsync(Guid userId, int page, int pageSize, string? matchType, string? result, DateTime? from, DateTime? to)
    {
        ValidatePage(ref page, ref pageSize);
        var query = _context.PvpMatches.AsNoTracking().Where(x => x.PvpMatchPlayers.Any(p => p.UserId == userId && (result == null || p.ResultCode == result)));
        if (!string.IsNullOrWhiteSpace(matchType)) query = query.Where(x => x.MatchTypeCode == matchType);
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

    public async Task<List<PvpRewardRuleResponse>> GetRewardRulesAsync() => await _context.PvpRewardRules.AsNoTracking().Include(x => x.RewardPackage).Select(x => new PvpRewardRuleResponse { MatchTypeCode = x.MatchTypeCode, ResultCode = x.ResultCode, WalletAmount = x.RewardPackage.WalletAmount, IsActive = x.IsActive }).ToListAsync();

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
        await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
        var now = DateTime.UtcNow;
        var invites = await _context.PvpSprintInvites.Where(x => x.StatusCode == "pending" && x.ExpiresAt <= now).ToListAsync(cancellationToken);
        foreach (var invite in invites) await ExpireInviteAsync(invite, now);

        var queues = await _context.MatchmakingQueues.Where(x => x.StatusCode == "waiting" && x.QueuedAt <= now.AddSeconds(-15)).ToListAsync(cancellationToken);
        foreach (var queue in queues)
        {
            if (!await IsActiveUserAsync(queue.UserId)) { _context.MatchmakingQueues.Remove(queue); RemoveActivity(queue.UserId); continue; }
            var playerProfile = await EnsureProfileAsync(queue.UserId, now);
            var bot = await _context.PvpBotProfiles.Where(x => x.IsActive).OrderBy(x => Math.Abs(x.Mmr - playerProfile.Mmr)).FirstOrDefaultAsync(cancellationToken);
            if (bot == null) continue;
            _context.MatchmakingQueues.Remove(queue);
            RemoveActivity(queue.UserId);
            var match = await CreateMatchAsync(queue.UserId, null, bot, "ranked", "bot", now);
            AddMatchActivity(queue.UserId, match, now);
            AddMatchOutbox(match, "match.created");
        }

        var countdown = await _context.PvpMatches.Where(x => x.StatusCode == "countdown" && x.CountdownEndsAt <= now).ToListAsync(cancellationToken);
        foreach (var match in countdown)
        {
            match.StatusCode = "running"; match.StartedAt = now; match.EndedAt = now.AddSeconds(30);
            if (_realtimeEnabled) await ApplySpiritPassivesAsync(match, now, cancellationToken);
            foreach (var activity in await _context.PvpPlayerActivities.Where(x => x.ActivityId == match.MatchId).ToListAsync(cancellationToken)) { activity.ActivityType = "match_running"; activity.DueAt = match.EndedAt; activity.UpdatedAt = now; }
            AddMatchOutbox(match, "match.started");
        }
        if (_realtimeEnabled)
        {
            await ProcessBotItemActionsAsync(now, cancellationToken);
            await ProcessEffectExpirationsAsync(now, cancellationToken);
        }
        var running = await _context.PvpMatches.Where(x => x.StatusCode == "running" && x.EndedAt <= now).ToListAsync(cancellationToken);
        foreach (var match in running)
        {
            match.StatusCode = "settling"; match.SettlementEndsAt = now.AddSeconds(10);
            foreach (var activity in await _context.PvpPlayerActivities.Where(x => x.ActivityId == match.MatchId).ToListAsync(cancellationToken)) { activity.ActivityType = "match_settling"; activity.DueAt = match.SettlementEndsAt; activity.UpdatedAt = now; }
        }
        var settling = await _context.PvpMatches.Include(x => x.PvpMatchPlayers).Where(x => x.StatusCode == "settling" && x.SettlementEndsAt <= now).ToListAsync(cancellationToken);
        foreach (var match in settling) await ResolveMatchAsync(match, now, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        });
    }

    private async Task ResolveMatchAsync(PvpMatch match, DateTime now, CancellationToken cancellationToken)
    {
        var players = match.PvpMatchPlayers.OrderBy(x => x.JoinedAt).ToList();
        if (players.Count != 2) { match.StatusCode = "cancelled"; return; }
        var bot = players.SingleOrDefault(x => x.ParticipantTypeCode == "bot");
        if (bot?.BotProfileId != null)
        {
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
        foreach (var player in players.Where(x => x.UserId.HasValue)) await CreateEntitlementAsync(match, player, now, cancellationToken);
        match.StatusCode = "finished"; match.ResolvedAt = now;
        foreach (var player in players.Where(x => x.UserId.HasValue)) RemoveActivity(player.UserId!.Value);
        AddMatchOutbox(match, "match.finished");
    }

    private async Task CreateEntitlementAsync(PvpMatch match, PvpMatchPlayer player, DateTime now, CancellationToken cancellationToken)
    {
        var rule = await _context.PvpRewardRules.Include(x => x.RewardPackage).FirstOrDefaultAsync(x => x.MatchTypeCode == match.MatchTypeCode && x.ResultCode == player.ResultCode && x.IsActive, cancellationToken);
        if (rule == null) return;
        var entitlement = new PvpMatchRewardEntitlement { MatchRewardEntitlementId = Guid.NewGuid(), MatchId = match.MatchId, UserId = player.UserId!.Value, ResultCode = player.ResultCode!, WalletAmount = rule.RewardPackage.WalletAmount, CreatedAt = now };
        _context.PvpMatchRewardEntitlements.Add(entitlement);
        var items = await _context.RewardPackageItems.Where(x => x.RewardPackageId == rule.RewardPackageId).ToListAsync(cancellationToken);
        foreach (var item in items) entitlement.Items.Add(new PvpMatchRewardItem { MatchRewardEntitlementId = entitlement.MatchRewardEntitlementId, ItemId = item.ItemId, Quantity = item.Quantity });
    }

    private async Task<PvpMatch> CreateMatchAsync(Guid firstUserId, Guid? secondUserId, PvpBotProfile? bot, string matchType, string source, DateTime now)
    {
        var firstProfile = await EnsureProfileAsync(firstUserId, now);
        var match = new PvpMatch { MatchId = Guid.NewGuid(), MatchTypeCode = matchType, SourceCode = source, StatusCode = "countdown", CreatedAt = now, CountdownEndsAt = now.AddSeconds(5), RatingK = RatingK, RatingDivisor = RatingDivisor, SpeedMinBps = 7500, SpeedMaxBps = 12500, ItemSlotLimit = 2, RuleVersion = 1 };
        _context.PvpMatches.Add(match);
        var firstPet = await GetPetSnapshotAsync(firstUserId);
        var firstPlayer = new PvpMatchPlayer { MatchPlayerId = Guid.NewGuid(), MatchId = match.MatchId, UserId = firstUserId, ParticipantTypeCode = "user", StepsAtMatch = 0, PetLevelAtMatch = firstPet.Level, PetIdSnapshot = firstPet.PetId, SpiritAffinityCode = firstPet.AffinityCode, Score = 0, MmrBefore = firstProfile.Mmr, IsReady = true, JoinedAt = now };
        _context.PvpMatchPlayers.Add(firstPlayer);
        await SnapshotPlayerLoadoutAsync(match, firstPlayer, firstUserId, now);
        if (secondUserId.HasValue)
        {
            var secondProfile = await EnsureProfileAsync(secondUserId.Value, now);
            var secondPet = await GetPetSnapshotAsync(secondUserId.Value);
            var secondPlayer = new PvpMatchPlayer { MatchPlayerId = Guid.NewGuid(), MatchId = match.MatchId, UserId = secondUserId, ParticipantTypeCode = "user", StepsAtMatch = 0, PetLevelAtMatch = secondPet.Level, PetIdSnapshot = secondPet.PetId, SpiritAffinityCode = secondPet.AffinityCode, Score = 0, MmrBefore = secondProfile.Mmr, IsReady = true, JoinedAt = now };
            _context.PvpMatchPlayers.Add(secondPlayer);
            await SnapshotPlayerLoadoutAsync(match, secondPlayer, secondUserId.Value, now);
        }
        else if (bot != null)
        {
            var botPlayer = new PvpMatchPlayer { MatchPlayerId = Guid.NewGuid(), MatchId = match.MatchId, BotProfileId = bot.BotProfileId, ParticipantTypeCode = "bot", SpiritAffinityCode = bot.SpiritAffinityCode, StepsAtMatch = 0, PetLevelAtMatch = 1, Score = 0, MmrBefore = bot.Mmr, IsReady = true, JoinedAt = now };
            _context.PvpMatchPlayers.Add(botPlayer);
            await SnapshotBotLoadoutAsync(match, botPlayer, bot.BotProfileId, now);
        }
        AddMatchAssigned(firstUserId, match, now);
        if (secondUserId.HasValue) AddMatchAssigned(secondUserId.Value, match, now);
        return match;
    }

    private async Task<PvpMatch> GetMatchForUserAsync(Guid matchId, Guid userId)
    {
        var match = await _context.PvpMatches.Include(x => x.PvpMatchPlayers).ThenInclude(x => x.User).ThenInclude(x => x!.UserProfile).Include(x => x.PvpMatchPlayers).ThenInclude(x => x.BotProfile).FirstOrDefaultAsync(x => x.MatchId == matchId)
            ?? throw new NotFoundException("Sprint match not found.");
        if (!match.PvpMatchPlayers.Any(x => x.UserId == userId)) throw new ForbiddenException("You are not a participant in this sprint match.");
        return match;
    }

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
            slot.Quantity = await _context.InventoryItems.AsNoTracking().Where(x => x.UserId == userId && x.ItemId == slot.ItemId).Select(x => (int?)x.Quantity).FirstOrDefaultAsync() ?? 0;
        return new PvpMatchResponse
        {
            MatchId = match.MatchId, MatchTypeCode = match.MatchTypeCode, StatusCode = match.StatusCode, CreatedAt = match.CreatedAt,
            CountdownEndsAt = match.CountdownEndsAt, StartedAt = match.StartedAt, EndedAt = match.EndedAt, SettlementEndsAt = match.SettlementEndsAt,
            ServerTime = now, RuleVersion = match.RuleVersion, ActiveEffects = activeEffects.Select(ToEffectResponse).ToList(), Loadout = loadout,
            Participants = match.PvpMatchPlayers.Select(x =>
            {
                var effects = activeEffects.Where(e => e.TargetMatchPlayerId == x.MatchPlayerId && e.EffectKindCode is "buff" or "debuff").Select(e => (e.EffectKindCode, e.MagnitudeBps));
                return new PvpParticipantResponse { ParticipantTypeCode = x.ParticipantTypeCode, UserId = x.UserId, BotProfileId = x.BotProfileId, DisplayName = x.User?.UserProfile?.Username ?? x.BotProfile?.DisplayName ?? "Player", AvatarUrl = x.User?.UserProfile?.AvatarUrl ?? x.BotProfile?.AvatarUrl, Score = x.Score, ValidatedSteps = x.ValidatedSteps, DistanceUnits = x.DistanceUnits, SpiritAffinityCode = x.SpiritAffinityCode, PassiveSpeedBps = x.PassiveSpeedBps, SpeedMultiplierBps = PvpGameplayCalculator.CalculateSpeedBps(x.PassiveSpeedBps, effects, match.SpeedMinBps, match.SpeedMaxBps), ResultCode = x.ResultCode };
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
        var existing = _context.PvpPlayerActivities.Local.FirstOrDefault(x => x.UserId == userId);
        if (existing != null)
        {
            existing.ActivityType = "match_countdown";
            existing.ActivityId = match.MatchId;
            existing.DueAt = match.CountdownEndsAt;
            existing.UpdatedAt = now;
            if (_context.Entry(existing).State == EntityState.Deleted) _context.Entry(existing).State = EntityState.Modified;
            return;
        }
        AddActivity(userId, "match_countdown", match.MatchId, match.CountdownEndsAt, now);
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
    private void AddOutbox(string aggregateType, Guid aggregateId, string eventType, object payload) => _context.OutboxEvents.Add(new OutboxEvent { EventId = Guid.NewGuid(), AggregateType = aggregateType, AggregateId = aggregateId, Destination = "signalr", EventType = eventType, PayloadJson = JsonSerializer.Serialize(payload), CreatedAt = DateTime.UtcNow });
    private void AddMatchAssigned(Guid userId, PvpMatch match, DateTime now) =>
        AddOutbox("user", userId, "match.assigned", new
        {
            matchId = match.MatchId,
            matchTypeCode = match.MatchTypeCode,
            sourceCode = match.SourceCode,
            statusCode = match.StatusCode,
            countdownEndsAt = match.CountdownEndsAt,
            serverTime = now
        });
    private void AddMatchOutbox(PvpMatch match, string eventType)
    {
        var now = DateTime.UtcNow;
        var sequence = ++match.LastEventSequence;
        var payload = JsonSerializer.Serialize(new { matchId = match.MatchId, status = match.StatusCode, sequence, serverTime = now, details = new { } });
        _context.PvpMatchEvents.Add(new PvpMatchEvent { PvpMatchEventId = Guid.NewGuid(), MatchId = match.MatchId, Sequence = sequence, EventType = eventType, PayloadJson = payload, CreatedAt = now });
        _context.OutboxEvents.Add(new OutboxEvent { EventId = Guid.NewGuid(), AggregateType = "match", AggregateId = match.MatchId, Destination = "signalr", EventType = eventType, PayloadJson = payload, CreatedAt = now });
    }
    private static void ValidatePage(ref int page, ref int pageSize) { page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100); }
}
