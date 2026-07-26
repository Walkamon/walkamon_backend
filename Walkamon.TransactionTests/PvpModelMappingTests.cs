using DAL.Data;
using DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Walkamon.TransactionTests;

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

    private static WalkamonContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WalkamonContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=WalkamonModelOnly;Integrated Security=true")
            .Options;
        return new WalkamonContext(options, new HttpContextAccessor());
    }
}
