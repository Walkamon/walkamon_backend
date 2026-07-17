using BLL.Service;
using DAL.Models;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class PvpEffectEngineTests
{
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _opponent = Guid.NewGuid();

    [Fact]
    public void Slow_WhenShieldAlreadyActive_IsBlockedAndConsumesThatShield()
    {
        var shield = Effect(_opponent, "pvp_shield", "shield");

        var result = PvpEffectEngine.Resolve("pvp_speed_down", _actor, _opponent, [shield]);

        Assert.True(result.CanApply);
        Assert.Equal("blocked", result.ResultCode);
        Assert.Equal(shield.PvpMatchEffectId, result.ConsumedShieldId);
    }

    [Fact]
    public void Shield_WhenSlowAlreadyActive_DoesNotCleanseExistingSlow()
    {
        var slow = Effect(_actor, "pvp_speed_down", "debuff");

        var result = PvpEffectEngine.Resolve("pvp_shield", _actor, _opponent, [slow]);

        Assert.True(result.CanApply);
        Assert.Equal("applied", result.ResultCode);
        Assert.Equal("shield", result.EffectKindCode);
        Assert.Empty(result.CleansedEffectIds);
    }

    [Fact]
    public void Cleanse_WithDebuffs_CleansesEveryActiveDebuff()
    {
        var slow1 = Effect(_actor, "pvp_speed_down", "debuff");
        var slow2 = Effect(_actor, "event_slow", "debuff");

        var result = PvpEffectEngine.Resolve("pvp_cleanse", _actor, _actor, [slow1, slow2]);

        Assert.True(result.CanApply);
        Assert.Equal("cleansed", result.ResultCode);
        Assert.Equal([slow1.PvpMatchEffectId, slow2.PvpMatchEffectId], result.CleansedEffectIds);
    }

    [Fact]
    public void Cleanse_WithoutDebuff_IsRejectedWithoutSideEffects()
    {
        var result = PvpEffectEngine.Resolve("pvp_cleanse", _actor, _actor, []);

        Assert.False(result.CanApply);
        Assert.Equal("There is no active debuff to cleanse.", result.ConflictMessage);
        Assert.Empty(result.CleansedEffectIds);
    }

    [Theory]
    [InlineData("pvp_speed_up", "buff")]
    [InlineData("pvp_shield", "shield")]
    public void NonStackingSelfEffect_WhenAlreadyActive_IsRejected(string effectCode, string kind)
    {
        var existing = Effect(_actor, effectCode, kind);

        var result = PvpEffectEngine.Resolve(effectCode, _actor, _actor, [existing]);

        Assert.False(result.CanApply);
    }

    private static PvpMatchEffect Effect(Guid target, string code, string kind) => new()
    {
        PvpMatchEffectId = Guid.NewGuid(),
        TargetMatchPlayerId = target,
        EffectCode = code,
        EffectKindCode = kind,
        StatusCode = "active",
        StartsAt = DateTime.UtcNow.AddSeconds(-1),
        EndsAt = DateTime.UtcNow.AddSeconds(5)
    };
}
