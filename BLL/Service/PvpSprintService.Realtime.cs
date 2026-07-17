using System.Data;
using System.Text.Json;
using BLL.Exceptions;
using DAL.DTO;
using DAL.Extensions;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace BLL.Service;

public sealed partial class PvpSprintService
{
    private static readonly string[] RequiredEffectCodes =
        ["pvp_speed_up", "pvp_speed_down", "pvp_cleanse", "pvp_shield"];

    public async Task<PvpLoadoutResponse> GetLoadoutAsync(Guid userId)
    {
        EnsureRealtimeEnabled();
        await EnsureActiveUserAsync(userId);
        return await BuildPlayerLoadoutResponseAsync(userId);
    }

    public async Task<PvpLoadoutResponse> UpdateLoadoutAsync(Guid userId, UpdatePvpLoadoutRequest request)
    {
        EnsureRealtimeEnabled();
        if (request.Slots.Count > 2 || request.Slots.Any(x => x.SlotNo is < 1 or > 2))
            throw new BadRequestException("PvP loadout supports slot 1 and slot 2 only.");
        if (request.Slots.Select(x => x.SlotNo).Distinct().Count() != request.Slots.Count ||
            request.Slots.Select(x => x.ItemId).Distinct().Count() != request.Slots.Count)
            throw new BadRequestException("PvP loadout slots and items must be unique.");

        await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
        await EnsureActiveUserAsync(userId);
        var itemIds = request.Slots.Select(x => x.ItemId).ToList();
        var definitions = await _context.PvpItemEffectDefinitions
            .Where(x => itemIds.Contains(x.ItemId) && x.IsActive)
            .ToListAsync();
        if (definitions.Count != itemIds.Count || definitions.Select(x => x.EffectCode).Distinct().Count() != definitions.Count)
            throw new BadRequestException("Loadout contains an invalid or duplicate PvP effect.");
        var owned = await _context.InventoryItems
            .Where(x => x.UserId == userId && itemIds.Contains(x.ItemId) && x.Quantity > 0)
            .Select(x => x.ItemId)
            .ToListAsync();
        if (owned.Count != itemIds.Count) throw new ConflictException("Every loadout item must exist in inventory.");

        var existing = await _context.PvpPlayerLoadoutSlots.Where(x => x.UserId == userId).ToListAsync();
        _context.PvpPlayerLoadoutSlots.RemoveRange(existing);
        var now = DateTime.UtcNow;
        foreach (var slot in request.Slots)
            _context.PvpPlayerLoadoutSlots.Add(new PvpPlayerLoadoutSlot { UserId = userId, SlotNo = slot.SlotNo, ItemId = slot.ItemId, UpdatedAt = now });
        await _context.SaveChangesAsync();
        });
        return await BuildPlayerLoadoutResponseAsync(userId);
    }

    public async Task<UsePvpItemResponse> UseItemAsync(Guid userId, Guid matchId, UsePvpItemRequest request)
    {
        EnsureRealtimeEnabled();
        if (request.SlotNo is < 1 or > 2 || request.ClientActionId == Guid.Empty)
            throw new BadRequestException("SlotNo and ClientActionId are required.");
        return await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable, async () =>
        {
        var duplicate = await _context.PvpMatchItemActions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MatchId == matchId && x.ClientActionId == request.ClientActionId && x.Actor.UserId == userId);
        if (duplicate != null)
        {
            return await BuildItemActionResponseAsync(duplicate, userId);
        }

        var match = await _context.PvpMatches
            .Include(x => x.PvpMatchPlayers)
            .Include(x => x.Effects)
            .Include(x => x.LoadoutSlots).ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.MatchId == matchId)
            ?? throw new NotFoundException("Sprint match not found.");
        var actor = match.PvpMatchPlayers.SingleOrDefault(x => x.UserId == userId)
            ?? throw new ForbiddenException("You are not a participant in this sprint match.");
        var now = DateTime.UtcNow;
        if (match.StatusCode != "running" || !match.EndedAt.HasValue || now >= match.EndedAt.Value)
            throw new ConflictException("PvP items can only be used while the match is running.");
        var slot = match.LoadoutSlots.SingleOrDefault(x => x.MatchPlayerId == actor.MatchPlayerId && x.SlotNo == request.SlotNo)
            ?? throw new NotFoundException("PvP loadout slot not found for this match.");
        if (slot.UsedAt.HasValue) throw new ConflictException("This PvP loadout slot has already been used.");
        var target = slot.TargetCode == "opponent"
            ? match.PvpMatchPlayers.Single(x => x.MatchPlayerId != actor.MatchPlayerId)
            : actor;
        var active = match.Effects.Where(x => x.StatusCode == "active" && x.EndsAt > now).ToList();
        var resolution = PvpEffectEngine.Resolve(
            slot.EffectCode,
            actor.MatchPlayerId,
            target.MatchPlayerId,
            active);
        if (!resolution.CanApply)
            throw new ConflictException(resolution.ConflictMessage ?? "PvP effect cannot be applied.");

        var decremented = await _context.InventoryItems
            .Where(x => x.UserId == userId && x.ItemId == slot.ItemId && x.Quantity > 0)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Quantity, x => x.Quantity - 1));
        if (decremented != 1)
            throw new ConflictException("PvP item is no longer available in inventory.");
        slot.UsedAt = now;
        var action = new PvpMatchItemAction
        {
            PvpMatchItemActionId = Guid.NewGuid(), MatchId = matchId, ActorMatchPlayerId = actor.MatchPlayerId,
            TargetMatchPlayerId = target.MatchPlayerId, MatchLoadoutSlotId = slot.PvpMatchLoadoutSlotId,
            ClientActionId = request.ClientActionId, EffectCode = slot.EffectCode, ResultCode = "applied", CreatedAt = now
        };
        _context.PvpMatchItemActions.Add(action);
        PvpMatchEffect? effect = null;
        if (resolution.ConsumedShieldId.HasValue)
        {
            var shield = active.Single(x => x.PvpMatchEffectId == resolution.ConsumedShieldId.Value);
            shield.StatusCode = "consumed";
            shield.ConsumedAt = now;
        }
        else if (resolution.CleansedEffectIds.Count > 0)
        {
            foreach (var debuff in active.Where(x => resolution.CleansedEffectIds.Contains(x.PvpMatchEffectId)))
            {
                debuff.StatusCode = "cleansed";
                debuff.ConsumedAt = now;
            }
        }
        else if (resolution.EffectKindCode != null)
            effect = CreateTimedEffect(match, actor, target, action, slot, resolution.EffectKindCode, now);
        action.ResultCode = resolution.ResultCode;

        AddMatchOutboxDetailed(match, "match.item.used", new { actionId = action.PvpMatchItemActionId, actor = actor.MatchPlayerId, target = target.MatchPlayerId, slot = slot.SlotNo, effect = slot.EffectCode, result = action.ResultCode, occurredAt = now });
        AddMatchOutboxDetailed(match, action.ResultCode switch { "blocked" => "match.effect.blocked", "cleansed" => "match.effect.cleansed", _ => "match.effect.applied" }, new { actionId = action.PvpMatchItemActionId, effectId = effect?.PvpMatchEffectId, actor = actor.MatchPlayerId, target = target.MatchPlayerId, effect = slot.EffectCode, magnitudeBps = slot.MagnitudeBps, endsAt = effect?.EndsAt, occurredAt = now });
        await _context.SaveChangesAsync();
        return await BuildItemActionResponseAsync(action, userId);
        });
    }

    public async Task<PvpProfileResponse> GetProfileAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var profile = await EnsureProfileAsync(userId, now);
        if (_context.Entry(profile).State == EntityState.Added) await _context.SaveChangesAsync();
        var tiers = await _context.PvpRankTiers.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        var tier = PvpGameplayCalculator.ResolveTier(profile.Mmr, tiers);
        var orderedIds = await ActiveRankingQuery().Select(x => x.UserId).ToListAsync();
        return new PvpProfileResponse { UserId = userId, Mmr = profile.Mmr, Position = orderedIds.IndexOf(userId) + 1, Tier = ToTierResponse(tier) };
    }

    public async Task<PvpPagedResponse<PvpRankingEntryResponse>> GetRankingsAsync(Guid userId, int page, int pageSize)
    {
        ValidatePage(ref page, ref pageSize);
        var tiers = await _context.PvpRankTiers.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        var all = await ActiveRankingQuery().Select(x => new { x.UserId, x.Mmr, x.User.UserProfile!.Username, x.User.UserProfile.AvatarUrl }).ToListAsync();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).Select((x, index) => new PvpRankingEntryResponse
        {
            UserId = x.UserId, Username = x.Username ?? "Player", AvatarUrl = x.AvatarUrl, Mmr = x.Mmr,
            Position = (page - 1) * pageSize + index + 1, Tier = ToTierResponse(PvpGameplayCalculator.ResolveTier(x.Mmr, tiers))
        }).ToList();
        return new PvpPagedResponse<PvpRankingEntryResponse> { Page = page, PageSize = pageSize, Total = all.Count, Items = items };
    }

    public Task<List<PvpItemEffectAdminRequest>> GetItemEffectsAsync() => _context.PvpItemEffectDefinitions.AsNoTracking().OrderBy(x => x.EffectCode)
        .Select(x => new PvpItemEffectAdminRequest { EffectCode = x.EffectCode, MagnitudeBps = x.MagnitudeBps, DurationMs = x.DurationMs, CooldownMs = x.CooldownMs, AssetKey = x.AssetKey, IsActive = x.IsActive }).ToListAsync();

    public async Task UpdateItemEffectsAsync(UpdatePvpItemEffectsRequest request)
    {
        if (request.Effects.Count != 4 || !RequiredEffectCodes.Order().SequenceEqual(request.Effects.Select(x => x.EffectCode).Order()))
            throw new BadRequestException("Exactly four supported PvP item effects are required.");
        var definitions = await _context.PvpItemEffectDefinitions.ToListAsync();
        foreach (var dto in request.Effects)
        {
            if (dto.MagnitudeBps < 0 || dto.DurationMs < 0 || dto.CooldownMs < 0) throw new BadRequestException("Effect values cannot be negative.");
            var entity = definitions.Single(x => x.EffectCode == dto.EffectCode);
            entity.MagnitudeBps = dto.MagnitudeBps; entity.DurationMs = dto.DurationMs; entity.CooldownMs = dto.CooldownMs;
            entity.AssetKey = dto.AssetKey; entity.IsActive = dto.IsActive; entity.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }

    public Task<List<PvpSpiritRuleAdminRequest>> GetSpiritRulesAsync() => _context.PvpSpiritSpeedRules.AsNoTracking().OrderBy(x => x.AffinityCode)
        .Select(x => new PvpSpiritRuleAdminRequest { AffinityCode = x.AffinityCode, StartMinute = x.StartMinute, EndMinute = x.EndMinute, BonusBps = x.BonusBps, IsActive = x.IsActive }).ToListAsync();

    public async Task UpdateSpiritRulesAsync(UpdatePvpSpiritRulesRequest request)
    {
        var expected = new[] { "sprout", "dawn", "warm_sun", "moonlight" };
        if (request.Rules.Count != 4 || !expected.Order().SequenceEqual(request.Rules.Select(x => x.AffinityCode).Order())) throw new BadRequestException("Exactly four spirit rules are required.");
        var entities = await _context.PvpSpiritSpeedRules.ToListAsync();
        foreach (var dto in request.Rules)
        {
            if (dto.StartMinute is < 0 or > 1439 || dto.EndMinute is < 0 or > 1439 || dto.BonusBps < 0) throw new BadRequestException("Spirit rule values are invalid.");
            var entity = entities.Single(x => x.AffinityCode == dto.AffinityCode);
            entity.StartMinute = dto.StartMinute; entity.EndMinute = dto.EndMinute; entity.BonusBps = dto.BonusBps;
            entity.IsActive = dto.IsActive; entity.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }

    public Task<List<PvpRankTierAdminRequest>> GetRankTiersAsync() => _context.PvpRankTiers.AsNoTracking().OrderBy(x => x.SortOrder)
        .Select(x => new PvpRankTierAdminRequest { TierCode = x.TierCode, DisplayName = x.DisplayName, MinMmr = x.MinMmr, SortOrder = x.SortOrder, AssetKey = x.AssetKey, ColorHex = x.ColorHex, IsActive = x.IsActive }).ToListAsync();

    public async Task UpdateRankTiersAsync(UpdatePvpRankTiersRequest request)
    {
        if (request.Tiers.Count != 6 || request.Tiers.Select(x => x.MinMmr).Distinct().Count() != 6 || request.Tiers.Count(x => x.MinMmr == int.MinValue) != 1)
            throw new BadRequestException("Six unique rank tiers including the minimum tier are required.");
        var existing = await _context.PvpRankTiers.ToListAsync();
        foreach (var dto in request.Tiers)
        {
            var entity = existing.SingleOrDefault(x => x.TierCode == dto.TierCode) ?? throw new BadRequestException("Rank tier codes cannot be changed.");
            entity.DisplayName = dto.DisplayName; entity.MinMmr = dto.MinMmr; entity.SortOrder = dto.SortOrder;
            entity.AssetKey = dto.AssetKey; entity.ColorHex = dto.ColorHex; entity.IsActive = dto.IsActive;
        }
        await _context.SaveChangesAsync();
    }

    private IQueryable<PvpPlayerProfile> ActiveRankingQuery() => _context.PvpPlayerProfiles.AsNoTracking()
        .Where(x => x.User.StatusCode == "active" && x.User.DeletedAt == null && x.User.UserProfile != null)
        .OrderByDescending(x => x.Mmr).ThenBy(x => x.UpdatedAt).ThenBy(x => x.UserId);

    private void EnsureRealtimeEnabled()
    {
        if (!_realtimeEnabled) throw new ConflictException("PvP realtime effects are not enabled in this environment.");
    }

    private async Task<PvpLoadoutResponse> BuildPlayerLoadoutResponseAsync(Guid userId)
    {
        var slots = await _context.PvpPlayerLoadoutSlots.AsNoTracking().Where(x => x.UserId == userId)
            .Join(_context.PvpItemEffectDefinitions, x => x.ItemId, x => x.ItemId, (slot, definition) => new { slot, definition })
            .Join(_context.Items, x => x.slot.ItemId, x => x.ItemId, (x, item) => new { x.slot, x.definition, item })
            .GroupJoin(_context.InventoryItems.Where(x => x.UserId == userId), x => x.slot.ItemId, x => x.ItemId, (x, inventory) => new { x, inventory })
            .SelectMany(x => x.inventory.DefaultIfEmpty(), (x, inventory) => new PvpMatchLoadoutSlotResponse { SlotNo = x.x.slot.SlotNo, ItemId = x.x.item.ItemId, ItemName = x.x.item.ItemName, EffectCode = x.x.definition.EffectCode, AssetKey = x.x.definition.AssetKey, Quantity = inventory == null ? 0 : inventory.Quantity })
            .OrderBy(x => x.SlotNo).ToListAsync();
        return new PvpLoadoutResponse { Slots = slots };
    }

    private PvpMatchEffect CreateTimedEffect(PvpMatch match, PvpMatchPlayer actor, PvpMatchPlayer target, PvpMatchItemAction action, PvpMatchLoadoutSlot slot, string kind, DateTime now)
    {
        var endsAt = now.AddMilliseconds(slot.DurationMs);
        if (match.EndedAt.HasValue && endsAt > match.EndedAt.Value) endsAt = match.EndedAt.Value;
        var effect = new PvpMatchEffect { PvpMatchEffectId = Guid.NewGuid(), MatchId = match.MatchId, TargetMatchPlayerId = target.MatchPlayerId, SourceMatchPlayerId = actor.MatchPlayerId, SourceItemActionId = action.PvpMatchItemActionId, EffectCode = slot.EffectCode, EffectKindCode = kind, MagnitudeBps = slot.MagnitudeBps, StatusCode = "active", StartsAt = now, EndsAt = endsAt, CreatedAt = now };
        _context.PvpMatchEffects.Add(effect);
        return effect;
    }

    private async Task<UsePvpItemResponse> BuildItemActionResponseAsync(PvpMatchItemAction action, Guid userId)
    {
        var effect = await _context.PvpMatchEffects.AsNoTracking().FirstOrDefaultAsync(x => x.SourceItemActionId == action.PvpMatchItemActionId);
        var slot = await _context.PvpMatchLoadoutSlots.AsNoTracking().FirstAsync(x => x.PvpMatchLoadoutSlotId == action.MatchLoadoutSlotId);
        var remaining = await _context.InventoryItems.AsNoTracking().Where(x => x.UserId == userId && x.ItemId == slot.ItemId).Select(x => (int?)x.Quantity).FirstOrDefaultAsync() ?? 0;
        return new UsePvpItemResponse { ActionId = action.PvpMatchItemActionId, ClientActionId = action.ClientActionId, ResultCode = action.ResultCode, EffectCode = action.EffectCode, RemainingQuantity = remaining, ServerTime = DateTime.UtcNow, Effect = effect == null ? null : ToEffectResponse(effect) };
    }

    private static PvpMatchEffectResponse ToEffectResponse(PvpMatchEffect effect) => new() { EffectId = effect.PvpMatchEffectId, TargetMatchPlayerId = effect.TargetMatchPlayerId, EffectCode = effect.EffectCode, EffectKindCode = effect.EffectKindCode, MagnitudeBps = effect.MagnitudeBps, StartsAt = effect.StartsAt, EndsAt = effect.EndsAt };
    private static PvpRankTierResponse ToTierResponse(PvpRankTier tier) => new() { TierCode = tier.TierCode, DisplayName = tier.DisplayName, MinMmr = tier.MinMmr, AssetKey = tier.AssetKey, ColorHex = tier.ColorHex };

    private async Task<(Guid? PetId, byte Level, string AffinityCode)> GetPetSnapshotAsync(Guid userId)
    {
        var userPet = await _context.UserPets.AsNoTracking().Include(x => x.Pet).FirstOrDefaultAsync(x => x.UserId == userId);
        if (userPet == null) return (null, 1, "sprout");
        return (userPet.PetId, (byte)Math.Clamp(userPet.Level, 1, byte.MaxValue), userPet.Pet.PvpAffinityCode ?? "sprout");
    }

    private async Task SnapshotPlayerLoadoutAsync(PvpMatch match, PvpMatchPlayer player, Guid userId, DateTime now)
    {
        var slots = await _context.PvpPlayerLoadoutSlots.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.SlotNo).Take(match.ItemSlotLimit).ToListAsync();
        await SnapshotLoadoutAsync(match, player, slots.Select(x => (x.SlotNo, x.ItemId)), now);
    }

    private async Task SnapshotBotLoadoutAsync(PvpMatch match, PvpMatchPlayer player, Guid botProfileId, DateTime now)
    {
        var slots = await _context.PvpBotLoadoutSlots.AsNoTracking().Where(x => x.BotProfileId == botProfileId).OrderBy(x => x.SlotNo).Take(match.ItemSlotLimit).ToListAsync();
        await SnapshotLoadoutAsync(match, player, slots.Select(x => (x.SlotNo, x.ItemId)), now);
    }

    private async Task SnapshotLoadoutAsync(PvpMatch match, PvpMatchPlayer player, IEnumerable<(byte SlotNo, Guid ItemId)> source, DateTime now)
    {
        foreach (var item in source)
        {
            var definition = await _context.PvpItemEffectDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.ItemId == item.ItemId && x.IsActive);
            if (definition == null) continue;
            _context.PvpMatchLoadoutSlots.Add(new PvpMatchLoadoutSlot
            {
                PvpMatchLoadoutSlotId = Guid.NewGuid(), MatchId = match.MatchId, MatchPlayerId = player.MatchPlayerId,
                SlotNo = item.SlotNo, ItemId = item.ItemId, EffectCode = definition.EffectCode, TargetCode = definition.TargetCode,
                MagnitudeBps = definition.MagnitudeBps, DurationMs = definition.DurationMs, CooldownMs = definition.CooldownMs,
                AssetKey = definition.AssetKey
            });
        }
    }

    private async Task ApplySpiritPassivesAsync(PvpMatch match, DateTime now, CancellationToken cancellationToken)
    {
        var players = await _context.PvpMatchPlayers.Where(x => x.MatchId == match.MatchId).ToListAsync(cancellationToken);
        var rules = await _context.PvpSpiritSpeedRules.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        foreach (var player in players)
        {
            var rule = rules.FirstOrDefault(x => x.AffinityCode == player.SpiritAffinityCode);
            player.PassiveSpeedBps = rule != null && PvpGameplayCalculator.IsRuleActiveAtUtc(now, rule) ? rule.BonusBps : 0;
            if (player.PassiveSpeedBps <= 0 || !match.EndedAt.HasValue) continue;
            _context.PvpMatchEffects.Add(new PvpMatchEffect
            {
                PvpMatchEffectId = Guid.NewGuid(), MatchId = match.MatchId, TargetMatchPlayerId = player.MatchPlayerId,
                EffectCode = $"spirit_{player.SpiritAffinityCode}", EffectKindCode = "passive", MagnitudeBps = player.PassiveSpeedBps,
                StatusCode = "active", StartsAt = now, EndsAt = match.EndedAt.Value, CreatedAt = now
            });
        }
    }

    private async Task ProcessEffectExpirationsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var effects = await _context.PvpMatchEffects.Include(x => x.Match)
            .Where(x => x.StatusCode == "active" && x.EndsAt <= now).ToListAsync(cancellationToken);
        foreach (var effect in effects)
        {
            effect.StatusCode = "expired";
            AddMatchOutboxDetailed(effect.Match, "match.effect.expired", new { effectId = effect.PvpMatchEffectId, target = effect.TargetMatchPlayerId, effect = effect.EffectCode, occurredAt = now });
        }
    }

    private async Task ProcessBotItemActionsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var matches = await _context.PvpMatches.Include(x => x.PvpMatchPlayers).Include(x => x.Effects).Include(x => x.LoadoutSlots)
            .Where(x => x.StatusCode == "running" && x.StartedAt != null && x.EndedAt > now && x.PvpMatchPlayers.Any(p => p.ParticipantTypeCode == "bot"))
            .ToListAsync(cancellationToken);
        foreach (var match in matches)
        {
            var bot = match.PvpMatchPlayers.Single(x => x.ParticipantTypeCode == "bot");
            foreach (var slot in match.LoadoutSlots.Where(x => x.MatchPlayerId == bot.MatchPlayerId && x.UsedAt == null).OrderBy(x => x.SlotNo))
            {
                var due = match.StartedAt!.Value.AddSeconds(slot.SlotNo == 1 ? 8 : 18);
                if (now < due) continue;
                var target = slot.TargetCode == "opponent" ? match.PvpMatchPlayers.Single(x => x.MatchPlayerId != bot.MatchPlayerId) : bot;
                var active = match.Effects.Where(x => x.StatusCode == "active" && x.EndsAt > now).ToList();
                var resolution = PvpEffectEngine.Resolve(
                    slot.EffectCode,
                    bot.MatchPlayerId,
                    target.MatchPlayerId,
                    active);
                if (!resolution.CanApply) continue;

                slot.UsedAt = now;
                var action = new PvpMatchItemAction { PvpMatchItemActionId = Guid.NewGuid(), MatchId = match.MatchId, ActorMatchPlayerId = bot.MatchPlayerId, TargetMatchPlayerId = target.MatchPlayerId, MatchLoadoutSlotId = slot.PvpMatchLoadoutSlotId, ClientActionId = Guid.NewGuid(), ResultCode = "applied", EffectCode = slot.EffectCode, CreatedAt = now };
                _context.PvpMatchItemActions.Add(action);
                PvpMatchEffect? effect = null;
                if (resolution.ConsumedShieldId.HasValue)
                {
                    var shield = active.Single(x => x.PvpMatchEffectId == resolution.ConsumedShieldId.Value);
                    shield.StatusCode = "consumed";
                    shield.ConsumedAt = now;
                }
                else if (resolution.CleansedEffectIds.Count > 0)
                {
                    foreach (var debuff in active.Where(x => resolution.CleansedEffectIds.Contains(x.PvpMatchEffectId)))
                    {
                        debuff.StatusCode = "cleansed";
                        debuff.ConsumedAt = now;
                    }
                }
                else if (resolution.EffectKindCode != null)
                    effect = CreateTimedEffect(match, bot, target, action, slot, resolution.EffectKindCode, now);
                action.ResultCode = resolution.ResultCode;
                AddMatchOutboxDetailed(match, "match.item.used", new { actionId = action.PvpMatchItemActionId, actor = bot.MatchPlayerId, target = target.MatchPlayerId, slot = slot.SlotNo, effect = slot.EffectCode, result = action.ResultCode, occurredAt = now });
                AddMatchOutboxDetailed(match, action.ResultCode == "blocked" ? "match.effect.blocked" : action.ResultCode == "cleansed" ? "match.effect.cleansed" : "match.effect.applied", new { actionId = action.PvpMatchItemActionId, effectId = effect?.PvpMatchEffectId, actor = bot.MatchPlayerId, target = target.MatchPlayerId, effect = slot.EffectCode, magnitudeBps = slot.MagnitudeBps, endsAt = effect?.EndsAt, occurredAt = now });
            }
        }
    }

    private async Task CalculateBotDistanceAsync(PvpMatch match, PvpMatchPlayer bot, CancellationToken cancellationToken)
    {
        if (!match.StartedAt.HasValue || !match.EndedAt.HasValue || !bot.BotProfileId.HasValue) return;
        var profile = await _context.PvpBotProfiles.AsNoTracking().FirstAsync(x => x.BotProfileId == bot.BotProfileId, cancellationToken);
        var effects = await _context.PvpMatchEffects.AsNoTracking().Where(x => x.MatchId == match.MatchId && x.TargetMatchPlayerId == bot.MatchPlayerId && (x.EffectKindCode == "buff" || x.EffectKindCode == "debuff")).ToListAsync(cancellationToken);
        var points = new List<DateTime> { match.StartedAt.Value, match.EndedAt.Value };
        foreach (var effect in effects)
        {
            points.Add(effect.StartsAt < match.StartedAt.Value ? match.StartedAt.Value : effect.StartsAt);
            var effectiveEnd = effect.ConsumedAt.HasValue && effect.ConsumedAt < effect.EndsAt ? effect.ConsumedAt.Value : effect.EndsAt;
            points.Add(effectiveEnd > match.EndedAt.Value ? match.EndedAt.Value : effectiveEnd);
        }
        points = points.Where(x => x >= match.StartedAt.Value && x <= match.EndedAt.Value).Distinct().Order().ToList();
        decimal distance = 0;
        for (var i = 0; i < points.Count - 1; i++)
        {
            var start = points[i]; var end = points[i + 1]; if (end <= start) continue;
            var midpoint = start.AddTicks((end - start).Ticks / 2);
            var active = effects.Where(x => x.StartsAt <= midpoint && (x.ConsumedAt ?? x.EndsAt) > midpoint).Select(x => (x.EffectKindCode, x.MagnitudeBps));
            var multiplier = PvpGameplayCalculator.CalculateSpeedBps(bot.PassiveSpeedBps, active, match.SpeedMinBps, match.SpeedMaxBps);
            distance += (decimal)(end - start).TotalSeconds * profile.StepsPerSecond * multiplier;
        }
        bot.ValidatedSteps = (int)Math.Floor((decimal)(match.EndedAt.Value - match.StartedAt.Value).TotalSeconds * profile.StepsPerSecond);
        bot.DistanceUnits = (long)Math.Round(distance, MidpointRounding.AwayFromZero);
        bot.Score = (int)Math.Min(int.MaxValue, bot.DistanceUnits / PvpGameplayCalculator.DistanceUnitsPerStep);
    }

    private void AddMatchOutboxDetailed(PvpMatch match, string eventType, object details)
    {
        var now = DateTime.UtcNow;
        var sequence = ++match.LastEventSequence;
        var payload = JsonSerializer.Serialize(new { matchId = match.MatchId, status = match.StatusCode, sequence, serverTime = now, details });
        _context.PvpMatchEvents.Add(new PvpMatchEvent { PvpMatchEventId = Guid.NewGuid(), MatchId = match.MatchId, Sequence = sequence, EventType = eventType, PayloadJson = payload, CreatedAt = now });
        _context.OutboxEvents.Add(new OutboxEvent { EventId = Guid.NewGuid(), AggregateType = "match", AggregateId = match.MatchId, Destination = "signalr", EventType = eventType, PayloadJson = payload, CreatedAt = now });
    }
}
