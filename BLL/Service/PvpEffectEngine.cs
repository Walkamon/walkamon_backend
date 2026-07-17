using DAL.Models;

namespace BLL.Service;

public sealed record PvpEffectResolution(
    bool CanApply,
    string ResultCode,
    string? EffectKindCode,
    Guid? ConsumedShieldId,
    IReadOnlyList<Guid> CleansedEffectIds,
    string? ConflictMessage)
{
    public static PvpEffectResolution Conflict(string message) =>
        new(false, "rejected", null, null, [], message);
}

public static class PvpEffectEngine
{
    public static PvpEffectResolution Resolve(
        string effectCode,
        Guid actorMatchPlayerId,
        Guid targetMatchPlayerId,
        IEnumerable<PvpMatchEffect> activeEffects)
    {
        var active = activeEffects.ToList();
        switch (effectCode)
        {
            case "pvp_speed_up":
                return active.Any(x =>
                    x.TargetMatchPlayerId == actorMatchPlayerId &&
                    x.EffectCode == effectCode)
                    ? PvpEffectResolution.Conflict("Speed buff is already active.")
                    : new(true, "applied", "buff", null, [], null);

            case "pvp_shield":
                return active.Any(x =>
                    x.TargetMatchPlayerId == actorMatchPlayerId &&
                    x.EffectCode == effectCode)
                    ? PvpEffectResolution.Conflict("Shield is already active.")
                    : new(true, "applied", "shield", null, [], null);

            case "pvp_cleanse":
            {
                var debuffs = active
                    .Where(x =>
                        x.TargetMatchPlayerId == actorMatchPlayerId &&
                        x.EffectKindCode == "debuff")
                    .Select(x => x.PvpMatchEffectId)
                    .ToList();
                return debuffs.Count == 0
                    ? PvpEffectResolution.Conflict("There is no active debuff to cleanse.")
                    : new(true, "cleansed", null, null, debuffs, null);
            }

            case "pvp_speed_down":
            {
                var shield = active.FirstOrDefault(x =>
                    x.TargetMatchPlayerId == targetMatchPlayerId &&
                    x.EffectCode == "pvp_shield");
                return shield == null
                    ? new(true, "applied", "debuff", null, [], null)
                    : new(true, "blocked", null, shield.PvpMatchEffectId, [], null);
            }

            default:
                return PvpEffectResolution.Conflict("Unsupported PvP item effect.");
        }
    }
}
