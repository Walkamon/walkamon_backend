using DAL.Data;
using DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Walkamon.TransactionTests;

[Trait("UC", "UC-67")]
[Trait("UC", "UC-72")]
public sealed class PvpModelMappingTests
{
    [Fact]
    public void PvpMatchPlayer_AllowsBotParticipantWithoutUserId()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(PvpMatchPlayer))!;

        Assert.Equal(
            nameof(PvpMatchPlayer.MatchPlayerId),
            Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        Assert.True(entity.FindProperty(nameof(PvpMatchPlayer.UserId))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(PvpMatchPlayer.BotProfileId))!.IsNullable);
    }

    [Fact]
    public void MatchRewardSnapshot_HasUniqueMatchResultIndexAndItemNavigation()
    {
        using var context = CreateContext();
        var snapshot = context.Model.FindEntityType(typeof(PvpMatchRewardSnapshot))!;
        var index = Assert.Single(
            snapshot.GetIndexes(),
            x => x.Properties.Select(p => p.Name)
                .SequenceEqual([
                    nameof(PvpMatchRewardSnapshot.MatchId),
                    nameof(PvpMatchRewardSnapshot.ResultCode)
                ]));

        Assert.True(index.IsUnique);
        Assert.NotNull(snapshot.FindNavigation(nameof(PvpMatchRewardSnapshot.Items)));
    }

    [Fact]
    public void ForfeitAndPetSnapshotProperties_MapToExpectedColumns()
    {
        using var context = CreateContext();
        var match = context.Model.FindEntityType(typeof(PvpMatch))!;
        var player = context.Model.FindEntityType(typeof(PvpMatchPlayer))!;
        var bot = context.Model.FindEntityType(typeof(PvpBotProfile))!;

        Assert.Equal(
            "finish_reason_code",
            match.FindProperty(nameof(PvpMatch.FinishReasonCode))!.GetColumnName());
        Assert.Equal(
            "forfeited_by_user_id",
            match.FindProperty(nameof(PvpMatch.ForfeitedByUserId))!.GetColumnName());
        Assert.NotNull(match.FindNavigation(nameof(PvpMatch.ForfeitedByUser)));
        Assert.Equal(
            "pet_name_snapshot",
            player.FindProperty(nameof(PvpMatchPlayer.PetNameSnapshot))!.GetColumnName());
        Assert.Equal(
            "pet_stage_no_snapshot",
            player.FindProperty(nameof(PvpMatchPlayer.PetStageNoSnapshot))!.GetColumnName());
        Assert.Equal(
            "pet_stage_no",
            bot.FindProperty(nameof(PvpBotProfile.PetStageNo))!.GetColumnName());
    }

    private static WalkamonContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WalkamonContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=WalkamonModelOnly;Integrated Security=true")
            .Options;
        return new WalkamonContext(options, new HttpContextAccessor());
    }
}
