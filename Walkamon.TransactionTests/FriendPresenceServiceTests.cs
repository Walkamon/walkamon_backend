using System.Text.RegularExpressions;
using BLL.Interfaces;
using BLL.Service;
using DAL.Data;
using DAL.DTO;
using DAL.Interfaces;
using DAL.Models;
using DAL.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class FriendPresenceServiceTests
{
    [Fact]
    public async Task GetFriendList_CombinesSignalRPresenceWithPvpActivityStatus()
    {
        var busyUserId = Guid.NewGuid();
        var availableUserId = Guid.NewGuid();
        var offlineUserId = Guid.NewGuid();
        var friendRepository = new Mock<IFriendRepository>();
        friendRepository
            .Setup(x => x.GetFriendListAsync(It.IsAny<Guid>()))
            .ReturnsAsync(
            [
                new FriendDto
                {
                    UserId = busyUserId,
                    PvpAvailabilityCode = "busy"
                },
                new FriendDto
                {
                    UserId = availableUserId,
                    PvpAvailabilityCode = "available"
                },
                new FriendDto
                {
                    UserId = offlineUserId,
                    PvpAvailabilityCode = "busy"
                }
            ]);

        var presenceTracker = new PvpPresenceTracker();
        presenceTracker.RegisterConnection(busyUserId, "busy-connection");
        presenceTracker.RegisterConnection(availableUserId, "available-connection");
        var service = new FriendService(
            Mock.Of<IGenericRepository<FriendRequest>>(),
            Mock.Of<IGenericRepository<Friendship>>(),
            friendRepository.Object,
            presenceTracker);

        var result = (await service.GetFriendListAsync(Guid.NewGuid()))
            .ToDictionary(x => x.UserId);

        Assert.True(result[busyUserId].IsOnline);
        Assert.Equal("busy", result[busyUserId].PvpAvailabilityCode);
        Assert.True(result[availableUserId].IsOnline);
        Assert.Equal("available", result[availableUserId].PvpAvailabilityCode);
        Assert.False(result[offlineUserId].IsOnline);
        Assert.Equal("offline", result[offlineUserId].PvpAvailabilityCode);
    }

    [Fact]
    public async Task FriendRepository_ReadsBusyStateFromPvpPlayerActivities()
    {
        var databaseName = $"WalkamonFriendPresence_{Guid.NewGuid():N}";
        var master = "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true";
        var database = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
        var currentUserId = Guid.NewGuid();
        var busyFriendId = Guid.NewGuid();
        var availableFriendId = Guid.NewGuid();

        try
        {
            var freshSql = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Sql", "WalkamonFreshSchema.sql"));
            freshSql = freshSql
                .Replace(
                    "CREATE DATABASE Walkamon;",
                    $"CREATE DATABASE [{databaseName}];",
                    StringComparison.Ordinal)
                .Replace(
                    "USE Walkamon;",
                    $"USE [{databaseName}];",
                    StringComparison.Ordinal);
            await using (var connection = new SqlConnection(master))
            {
                await connection.OpenAsync();
                await ExecuteBatchesAsync(connection, freshSql);
            }

            await SeedUsersAsync(
                database,
                currentUserId,
                busyFriendId,
                availableFriendId);
            await using (var connection = new SqlConnection(database))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT dbo.friendships(user_low_id,user_high_id)
                    VALUES
                    (@first_low,@first_high),
                    (@second_low,@second_high);

                    INSERT dbo.pvp_player_activities(user_id,activity_type,activity_id)
                    VALUES(@busy_user_id,'queue_waiting',NEWID());
                    """;
                AddOrderedPair(command, "first", currentUserId, busyFriendId);
                AddOrderedPair(command, "second", currentUserId, availableFriendId);
                command.Parameters.AddWithValue("@busy_user_id", busyFriendId);
                await command.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<WalkamonContext>()
                .UseSqlServer(database)
                .Options;
            await using var context = new WalkamonContext(
                options,
                new HttpContextAccessor());
            var result = (await new FriendRepository(context)
                    .GetFriendListAsync(currentUserId))
                .ToDictionary(x => x.UserId);

            Assert.Equal("busy", result[busyFriendId].PvpAvailabilityCode);
            Assert.Equal("available", result[availableFriendId].PvpAvailabilityCode);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(master);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF DB_ID(N'{databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END
                """;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static void AddOrderedPair(
        SqlCommand command,
        string prefix,
        Guid first,
        Guid second)
    {
        var low = first.CompareTo(second) < 0 ? first : second;
        var high = first.CompareTo(second) < 0 ? second : first;
        command.Parameters.AddWithValue($"@{prefix}_low", low);
        command.Parameters.AddWithValue($"@{prefix}_high", high);
    }

    private static async Task SeedUsersAsync(
        string connectionString,
        params Guid[] userIds)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        for (var index = 0; index < userIds.Length; index++)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @role_id INT=(SELECT TOP(1) role_id FROM dbo.roles WHERE role_code='0');
                INSERT dbo.users(user_id,role_id,email,normalized_email,email_confirmed,status_code)
                VALUES(@user_id,@role_id,@email,@normalized_email,1,'active');
                INSERT dbo.user_profiles(user_id,username)
                VALUES(@user_id,@username);
                INSERT dbo.wallets(user_id,balance)
                VALUES(@user_id,0);
                """;
            command.Parameters.AddWithValue("@user_id", userIds[index]);
            command.Parameters.AddWithValue(
                "@email",
                $"friend-presence-{index}@walkamon.test");
            command.Parameters.AddWithValue(
                "@normalized_email",
                $"FRIEND-PRESENCE-{index}@WALKAMON.TEST");
            command.Parameters.AddWithValue(
                "@username",
                $"friend_presence_{index}");
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task ExecuteBatchesAsync(
        SqlConnection connection,
        string script)
    {
        foreach (var batch in Regex.Split(
                     script,
                     @"^\s*GO\s*;?\s*$",
                     RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch))
                continue;

            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 60;
            await command.ExecuteNonQueryAsync();
        }
    }
}
