using System.Text.RegularExpressions;
using BLL.Exceptions;
using BLL.Service;
using DAL.Data;
using DAL.GenericRepository;
using DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class PlayerChallengeIntegrationTests
{
    [Fact]
    public void ChallengeCycleDate_UsesVietnamCalendarBoundary()
    {
        Assert.Equal(
            new DateOnly(2026, 8, 4),
            ChallengeCycleDate.FromUtc(new DateTime(2026, 8, 4, 16, 59, 59, DateTimeKind.Utc)));
        Assert.Equal(
            new DateOnly(2026, 8, 5),
            ChallengeCycleDate.FromUtc(new DateTime(2026, 8, 4, 17, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task ChallengeLifecycle_RequiresAssignmentCompletesClaimsAndWaitsForNextRandom()
    {
        await using var scope = await ChallengeDatabaseScope.CreateAsync();
        var userId = Guid.NewGuid();
        await scope.SeedUserAsync(userId, 100);
        var catalog = await scope.SeedChallengeCatalogAsync(challengeCount: 2, walletAmount: 500, itemQuantity: 1);

        await using var context = scope.CreateContext();
        var progress = new MissionProgressService(context);

        // Matching activity must not silently assign every challenge in the catalog.
        await progress.AddProgressAsync(userId, "steps", 5);
        Assert.Empty(await context.UserMissions.Where(x => x.UserId == userId).ToListAsync());

        var service = CreateService(context);
        var assigned = await service.CreateRandomChallengeAsync(userId);
        Assert.NotNull(assigned.CurrentChallenge);
        Assert.Equal("active", assigned.CurrentChallenge!.StatusCode);
        Assert.False(assigned.CurrentChallenge.CanClaim);

        await progress.AddProgressAsync(userId, "steps", 5);
        var completed = await service.GetRandomChallengeStateAsync(userId);
        Assert.NotNull(completed.CurrentChallenge);
        Assert.Equal("completed", completed.CurrentChallenge!.StatusCode);
        Assert.True(completed.CurrentChallenge.CanClaim);
        Assert.False(completed.CurrentChallenge.IsCancelable);
        Assert.Null(completed.CurrentChallenge.ClaimedAt);

        var claimed = await service.ClaimChallengeRewardAsync(
            userId,
            completed.CurrentChallenge.UserMissionId);

        Assert.Equal("claimed", claimed.StatusCode);
        Assert.Equal(500, claimed.WalletAmount);
        Assert.Equal(600, claimed.WalletBalance);
        var rewardItem = Assert.Single(claimed.RewardItems);
        Assert.Equal(catalog.ItemId, rewardItem.ItemId);
        Assert.Equal(1, rewardItem.Quantity);

        var storedAssignment = await context.UserMissions
            .SingleAsync(x => x.UserMissionId == claimed.UserMissionId);
        Assert.Equal("claimed", storedAssignment.StatusCode);
        Assert.NotNull(storedAssignment.ClaimedAt);
        Assert.Equal(
            1,
            (await context.InventoryItems.SingleAsync(x =>
                x.UserId == userId && x.ItemId == catalog.ItemId)).Quantity);

        // Claimed challenge disappears, and progress remains frozen.
        Assert.Null((await service.GetRandomChallengeStateAsync(userId)).CurrentChallenge);
        await progress.AddProgressAsync(userId, "steps", 50);
        Assert.Equal(5, storedAssignment.ProgressValue);

        // The next challenge is created only after an explicit random request.
        var next = await service.CreateRandomChallengeAsync(userId);
        Assert.NotNull(next.CurrentChallenge);
        Assert.NotEqual(claimed.ChallengeId, next.CurrentChallenge!.ChallengeId);
    }

    [Fact]
    public async Task Claim_RejectsIncompleteCancelledExpiredDisabledForeignAndDuplicateAssignments()
    {
        await using var scope = await ChallengeDatabaseScope.CreateAsync();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await scope.SeedUserAsync(ownerId, 0);
        await scope.SeedUserAsync(otherId, 0);
        var catalog = await scope.SeedChallengeCatalogAsync(challengeCount: 4, walletAmount: 10, itemQuantity: 0);
        var missionIds = catalog.MissionIds;

        var incompleteId = await scope.SeedAssignmentAsync(ownerId, missionIds[0], "active", 4);
        var cancelledId = await scope.SeedAssignmentAsync(ownerId, missionIds[1], "cancelled", 5);
        var expiredId = await scope.SeedAssignmentAsync(ownerId, missionIds[2], "completed", 5);
        var disabledId = await scope.SeedAssignmentAsync(ownerId, missionIds[3], "completed", 5);
        await scope.UpdateMissionAsync(missionIds[2], isActive: true, endAt: DateTime.UtcNow.AddMinutes(-1));
        await scope.UpdateMissionAsync(missionIds[3], isActive: false, endAt: null);

        await using var context = scope.CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ClaimChallengeRewardAsync(ownerId, incompleteId));
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ClaimChallengeRewardAsync(ownerId, cancelledId));
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ClaimChallengeRewardAsync(ownerId, expiredId));
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ClaimChallengeRewardAsync(ownerId, disabledId));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ClaimChallengeRewardAsync(otherId, incompleteId));

        var validMission = await scope.SeedSingleChallengeAsync(walletAmount: 25, itemQuantity: 0);
        var validAssignment = await scope.SeedAssignmentAsync(ownerId, validMission.MissionId, "completed", 5);
        await service.ClaimChallengeRewardAsync(ownerId, validAssignment);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ClaimChallengeRewardAsync(ownerId, validAssignment));

        Assert.Equal(25, (await context.Wallets.SingleAsync(x => x.UserId == ownerId)).Balance);
    }

    [Fact]
    public async Task ConcurrentClaimAndRandom_ProduceExactlyOneCommittedResult()
    {
        await using var scope = await ChallengeDatabaseScope.CreateAsync();
        var claimUserId = Guid.NewGuid();
        var randomUserId = Guid.NewGuid();
        await scope.SeedUserAsync(claimUserId, 0);
        await scope.SeedUserAsync(randomUserId, 0);
        var catalog = await scope.SeedChallengeCatalogAsync(challengeCount: 2, walletAmount: 75, itemQuantity: 2);
        var assignmentId = await scope.SeedAssignmentAsync(
            claimUserId,
            catalog.MissionIds[0],
            "completed",
            5);

        var claimResults = await Task.WhenAll(
            ExecuteClaimAsync(scope, claimUserId, assignmentId),
            ExecuteClaimAsync(scope, claimUserId, assignmentId));
        Assert.Single(claimResults, x => x == "success");
        Assert.Single(claimResults, x => x == "conflict");

        var randomResults = await Task.WhenAll(
            ExecuteRandomAsync(scope, randomUserId),
            ExecuteRandomAsync(scope, randomUserId));
        Assert.Single(randomResults, x => x == "success");
        Assert.Single(randomResults, x => x == "conflict");

        await using var verification = scope.CreateContext();
        Assert.Equal(75, (await verification.Wallets.SingleAsync(x => x.UserId == claimUserId)).Balance);
        Assert.Equal(
            2,
            (await verification.InventoryItems.SingleAsync(x =>
                x.UserId == claimUserId && x.ItemId == catalog.ItemId)).Quantity);
        Assert.Single(await verification.UserMissions.Where(x =>
            x.UserId == randomUserId
            && (x.StatusCode == "active" || x.StatusCode == "completed")).ToListAsync());
    }

    [Fact]
    public async Task Claim_SupportsItemOnlyAndRollsBackOverflow()
    {
        await using var scope = await ChallengeDatabaseScope.CreateAsync();
        var itemOnlyUserId = Guid.NewGuid();
        var walletOverflowUserId = Guid.NewGuid();
        var inventoryOverflowUserId = Guid.NewGuid();
        await scope.SeedUserAsync(itemOnlyUserId, 20);
        await scope.SeedUserAsync(walletOverflowUserId, int.MaxValue);
        await scope.SeedUserAsync(inventoryOverflowUserId, 0);

        var itemOnly = await scope.SeedSingleChallengeAsync(walletAmount: 0, itemQuantity: 3);
        var itemOnlyAssignment = await scope.SeedAssignmentAsync(
            itemOnlyUserId,
            itemOnly.MissionId,
            "completed",
            5);

        await using (var context = scope.CreateContext())
        {
            var result = await CreateService(context).ClaimChallengeRewardAsync(
                itemOnlyUserId,
                itemOnlyAssignment);
            Assert.Equal(20, result.WalletBalance);
            Assert.Equal(0, result.WalletAmount);
            Assert.Equal(3, Assert.Single(result.RewardItems).Quantity);
        }

        var walletOverflow = await scope.SeedSingleChallengeAsync(walletAmount: 1, itemQuantity: 0);
        var walletOverflowAssignment = await scope.SeedAssignmentAsync(
            walletOverflowUserId,
            walletOverflow.MissionId,
            "completed",
            5);
        await using (var context = scope.CreateContext())
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                CreateService(context).ClaimChallengeRewardAsync(
                    walletOverflowUserId,
                    walletOverflowAssignment));
        }

        var inventoryOverflow = await scope.SeedSingleChallengeAsync(walletAmount: 10, itemQuantity: 1);
        var inventoryOverflowAssignment = await scope.SeedAssignmentAsync(
            inventoryOverflowUserId,
            inventoryOverflow.MissionId,
            "completed",
            5);
        await scope.SeedInventoryAsync(
            inventoryOverflowUserId,
            inventoryOverflow.ItemId,
            int.MaxValue);
        await using (var context = scope.CreateContext())
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                CreateService(context).ClaimChallengeRewardAsync(
                    inventoryOverflowUserId,
                    inventoryOverflowAssignment));
        }

        await using var verification = scope.CreateContext();
        Assert.Equal(
            int.MaxValue,
            (await verification.Wallets.SingleAsync(x => x.UserId == walletOverflowUserId)).Balance);
        Assert.Equal(
            "completed",
            (await verification.UserMissions.SingleAsync(x =>
                x.UserMissionId == walletOverflowAssignment)).StatusCode);
        Assert.Equal(
            0,
            (await verification.Wallets.SingleAsync(x => x.UserId == inventoryOverflowUserId)).Balance);
        Assert.Equal(
            int.MaxValue,
            (await verification.InventoryItems.SingleAsync(x =>
                x.UserId == inventoryOverflowUserId
                && x.ItemId == inventoryOverflow.ItemId)).Quantity);
        Assert.Equal(
            "completed",
            (await verification.UserMissions.SingleAsync(x =>
                x.UserMissionId == inventoryOverflowAssignment)).StatusCode);
    }

    private static async Task<string> ExecuteClaimAsync(
        ChallengeDatabaseScope scope,
        Guid userId,
        Guid assignmentId)
    {
        await using var context = scope.CreateContext();
        try
        {
            await CreateService(context).ClaimChallengeRewardAsync(userId, assignmentId);
            return "success";
        }
        catch (ConflictException)
        {
            return "conflict";
        }
    }

    private static async Task<string> ExecuteRandomAsync(
        ChallengeDatabaseScope scope,
        Guid userId)
    {
        await using var context = scope.CreateContext();
        try
        {
            await CreateService(context).CreateRandomChallengeAsync(userId);
            return "success";
        }
        catch (ConflictException)
        {
            return "conflict";
        }
    }

    private static PlayerChallengeService CreateService(WalkamonContext context)
    {
        return new PlayerChallengeService(
            new GenericRepository<Mission>(context),
            new GenericRepository<UserMission>(context),
            new GenericRepository<RewardPackage>(context),
            new GenericRepository<RewardPackageItem>(context),
            new GenericRepository<Item>(context),
            context,
            new AchievementProgressService(context),
            new MissionProgressService(context));
    }

    private sealed class ChallengeDatabaseScope : IAsyncDisposable
    {
        private const string MasterConnectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true";

        private ChallengeDatabaseScope(string databaseName, string connectionString)
        {
            DatabaseName = databaseName;
            ConnectionString = connectionString;
        }

        private string DatabaseName { get; }
        private string ConnectionString { get; }

        public static async Task<ChallengeDatabaseScope> CreateAsync()
        {
            var databaseName = $"WalkamonChallenge_{Guid.NewGuid():N}";
            var connectionString =
                $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
            var script = await File.ReadAllTextAsync(Path.Combine(
                AppContext.BaseDirectory,
                "Sql",
                "WalkamonFreshSchema.sql"));
            script = script
                .Replace("CREATE DATABASE Walkamon;", $"CREATE DATABASE [{databaseName}];", StringComparison.Ordinal)
                .Replace("USE Walkamon;", $"USE [{databaseName}];", StringComparison.Ordinal);

            await using var connection = new SqlConnection(MasterConnectionString);
            await connection.OpenAsync();
            await ExecuteBatchesAsync(connection, script);
            return new ChallengeDatabaseScope(databaseName, connectionString);
        }

        public WalkamonContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<WalkamonContext>()
                .UseSqlServer(ConnectionString, sql => sql.EnableRetryOnFailure())
                .Options;
            return new WalkamonContext(options, new HttpContextAccessor());
        }

        public async Task SeedUserAsync(Guid userId, int walletBalance)
        {
            await ExecuteAsync(
                """
                DECLARE @role_id INT=(SELECT TOP(1) role_id FROM roles ORDER BY role_id);
                INSERT users(user_id,role_id,email,normalized_email,email_confirmed,status_code)
                VALUES(@user_id,@role_id,@email,@normalized_email,1,'active');
                INSERT wallets(user_id,balance) VALUES(@user_id,@balance);
                """,
                ("@user_id", userId),
                ("@email", $"{userId:N}@challenge.test"),
                ("@normalized_email", $"{userId:N}@CHALLENGE.TEST"),
                ("@balance", walletBalance));
        }

        public async Task<(Guid ItemId, IReadOnlyList<Guid> MissionIds)> SeedChallengeCatalogAsync(
            int challengeCount,
            int walletAmount,
            int itemQuantity)
        {
            var itemTypeId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var rewardPackageId = Guid.NewGuid();
            await ExecuteAsync(
                """
                INSERT item_types(item_type_id,item_type_name) VALUES(@item_type_id,@type_name);
                INSERT items(item_id,item_name,item_type_id,is_active)
                VALUES(@item_id,@item_name,@item_type_id,1);
                INSERT reward_packages(reward_package_id,package_name,wallet_amount)
                VALUES(@reward_package_id,@package_name,@wallet_amount);
                """,
                ("@item_type_id", itemTypeId),
                ("@type_name", $"Challenge type {itemTypeId:N}"),
                ("@item_id", itemId),
                ("@item_name", $"Challenge item {itemId:N}"),
                ("@reward_package_id", rewardPackageId),
                ("@package_name", $"Challenge package {rewardPackageId:N}"),
                ("@wallet_amount", walletAmount));

            if (itemQuantity > 0)
            {
                await ExecuteAsync(
                    "INSERT reward_package_items(reward_package_id,item_id,quantity) VALUES(@package,@item,@quantity);",
                    ("@package", rewardPackageId),
                    ("@item", itemId),
                    ("@quantity", itemQuantity));
            }

            var missionIds = new List<Guid>();
            for (var index = 0; index < challengeCount; index++)
            {
                var missionId = Guid.NewGuid();
                missionIds.Add(missionId);
                await ExecuteAsync(
                    """
                    INSERT missions(
                        mission_id,mission_type_code,title,metric_code,target_value,
                        reward_package_id,is_cancelable,is_active)
                    VALUES(@mission_id,'challenge',@title,'steps',5,@package,1,1);
                    """,
                    ("@mission_id", missionId),
                    ("@title", $"Challenge {missionId:N}"),
                    ("@package", rewardPackageId));
            }

            return (itemId, missionIds);
        }

        public async Task<(Guid MissionId, Guid ItemId)> SeedSingleChallengeAsync(
            int walletAmount,
            int itemQuantity)
        {
            var catalog = await SeedChallengeCatalogAsync(1, walletAmount, itemQuantity);
            return (catalog.MissionIds[0], catalog.ItemId);
        }

        public async Task<Guid> SeedAssignmentAsync(
            Guid userId,
            Guid missionId,
            string statusCode,
            int progress)
        {
            var assignmentId = Guid.NewGuid();
            await ExecuteAsync(
                """
                INSERT user_missions(
                    user_mission_id,user_id,mission_id,cycle_date,assigned_at,
                    progress_value,status_code,claimed_at)
                VALUES(
                    @assignment_id,@user_id,@mission_id,
                    CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())),SYSUTCDATETIME(),
                    @progress,@status,NULL);
                """,
                ("@assignment_id", assignmentId),
                ("@user_id", userId),
                ("@mission_id", missionId),
                ("@progress", progress),
                ("@status", statusCode));
            return assignmentId;
        }

        public Task UpdateMissionAsync(Guid missionId, bool isActive, DateTime? endAt)
        {
            return ExecuteAsync(
                "UPDATE missions SET is_active=@active,end_at=@end_at WHERE mission_id=@mission_id;",
                ("@active", isActive),
                ("@end_at", endAt ?? (object)DBNull.Value),
                ("@mission_id", missionId));
        }

        public Task SeedInventoryAsync(Guid userId, Guid itemId, int quantity)
        {
            return ExecuteAsync(
                "INSERT inventory_items(user_id,item_id,quantity) VALUES(@user_id,@item_id,@quantity);",
                ("@user_id", userId),
                ("@item_id", itemId),
                ("@quantity", quantity));
        }

        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(MasterConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF DB_ID(N'{DatabaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{DatabaseName}];
                END
                """;
            await command.ExecuteNonQueryAsync();
        }

        private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }
            await command.ExecuteNonQueryAsync();
        }

        private static async Task ExecuteBatchesAsync(SqlConnection connection, string script)
        {
            foreach (var batch in Regex.Split(
                         script,
                         @"^\s*GO\s*;?\s*$",
                         RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 60;
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
