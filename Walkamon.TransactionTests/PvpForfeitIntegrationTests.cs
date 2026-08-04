using System.Text.RegularExpressions;
using BLL.Exceptions;
using BLL.Interfaces;
using BLL.Options;
using BLL.Service;
using DAL.Data;
using DAL.DTO;
using DAL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Walkamon.TransactionTests;

[Trait("UC", "UC-69")]
[Trait("UC", "UC-72")]
public sealed class PvpForfeitIntegrationTests
{
    [Fact]
    public async Task Forfeit_CoversCountdownRunningBotRetryConcurrencyAndPetSnapshot()
    {
        var scope = await PvpDatabaseScope.CreateAsync();
        await using (scope)
        {
            var users = Enumerable.Range(0, 9).Select(_ => Guid.NewGuid()).ToArray();
            await scope.SeedUsersAsync(users);
            await scope.SeedPetAsync(users[0]);
            await scope.SeedPetAsync(users[1]);

            await using var context = scope.CreateContext();
            var presenceTracker = new PvpPresenceTracker();
            foreach (var userId in users)
                presenceTracker.RegisterConnection(userId, $"connection-{userId}");
            var service = CreateService(context, presenceTracker);
            await service.UpdateRewardRulesAsync(RewardMatrix());

            // Ranked human/human: forfeit during countdown settles immediately.
            await service.JoinMatchmakingAsync(users[0], new JoinPvpMatchmakingRequest());
            var assigned = await service.JoinMatchmakingAsync(users[1], new JoinPvpMatchmakingRequest());
            var rankedMatchId = Assert.IsType<Guid>(assigned.MatchId);
            var snapshotBeforeEvolution = await service.GetMatchAsync(users[0], rankedMatchId);
            var petParticipant = Assert.Single(
                snapshotBeforeEvolution.Participants,
                x => x.UserId == users[0]);
            Assert.Equal("Warm Test Spirit", petParticipant.PetName);
            Assert.Equal(12, petParticipant.PetLevel);
            Assert.Equal(2, petParticipant.PetStageNo);
            Assert.Equal("warm_sun_stage2", petParticipant.PetVisualCode);

            var quitterPlayerId = await context.PvpMatchPlayers
                .Where(x => x.MatchId == rankedMatchId && x.UserId == users[0])
                .Select(x => x.MatchPlayerId)
                .SingleAsync();
            var now = DateTime.UtcNow;
            var sessionId = Guid.NewGuid();
            var effectId = Guid.NewGuid();
            context.PvpStepSessions.Add(new PvpStepSession
            {
                StepSessionId = sessionId,
                MatchId = rankedMatchId,
                UserId = users[0],
                PurposeCode = "pvp",
                PlatformCode = "android",
                SensorModeCode = "detector",
                Nonce = "forfeit-test",
                StatusCode = "active",
                ExpiresAt = now.AddMinutes(1),
                CreatedAt = now
            });
            context.PvpMatchEffects.Add(new PvpMatchEffect
            {
                PvpMatchEffectId = effectId,
                MatchId = rankedMatchId,
                TargetMatchPlayerId = quitterPlayerId,
                EffectCode = "pvp_speed_up",
                EffectKindCode = "buff",
                MagnitudeBps = 1500,
                StatusCode = "active",
                StartsAt = now,
                EndsAt = now.AddSeconds(5),
                CreatedAt = now
            });
            await context.SaveChangesAsync();

            await scope.EvolvePetAfterSnapshotAsync(users[0]);
            context.ChangeTracker.Clear();

            var forfeitResult = await service.ForfeitMatchAsync(users[0], rankedMatchId);
            Assert.Equal("finished", forfeitResult.StatusCode);
            Assert.Equal("user_forfeit", forfeitResult.FinishReasonCode);
            Assert.Equal(users[0], forfeitResult.ForfeitedByUserId);
            Assert.Equal(users[1], forfeitResult.WinnerUserId);
            Assert.Equal(-16, forfeitResult.MmrDelta);
            Assert.Equal(984, forfeitResult.MmrAfter);
            Assert.False(forfeitResult.CanClaimReward);
            Assert.NotNull(forfeitResult.ResolvedAt);

            var snapshotAfterEvolution = Assert.Single(
                forfeitResult.Participants,
                x => x.UserId == users[0]);
            Assert.Equal("Warm Test Spirit", snapshotAfterEvolution.PetName);
            Assert.Equal(2, snapshotAfterEvolution.PetStageNo);
            Assert.Equal("warm_sun_stage2", snapshotAfterEvolution.PetVisualCode);

            var winnerProfile = await context.PvpPlayerProfiles.AsNoTracking()
                .SingleAsync(x => x.UserId == users[1]);
            Assert.Equal(1016, winnerProfile.Mmr);
            var entitlement = await context.PvpMatchRewardEntitlements.AsNoTracking()
                .SingleAsync(x => x.MatchId == rankedMatchId);
            Assert.Equal(users[1], entitlement.UserId);
            Assert.Equal("win", entitlement.ResultCode);
            Assert.Equal(30, entitlement.WalletAmount);
            Assert.False(await context.PvpMatchRewardEntitlements.AsNoTracking()
                .AnyAsync(x => x.MatchId == rankedMatchId && x.UserId == users[0]));
            Assert.False(await context.PvpPlayerActivities.AsNoTracking()
                .AnyAsync(x => x.ActivityId == rankedMatchId));

            var closedSession = await context.PvpStepSessions.AsNoTracking()
                .SingleAsync(x => x.StepSessionId == sessionId);
            Assert.Equal("closed", closedSession.StatusCode);
            Assert.Equal("user_forfeit", closedSession.ClosedReason);
            var expiredEffect = await context.PvpMatchEffects.AsNoTracking()
                .SingleAsync(x => x.PvpMatchEffectId == effectId);
            Assert.Equal("expired", expiredEffect.StatusCode);
            Assert.True(expiredEffect.EndsAt <= forfeitResult.ResolvedAt);

            var terminalEvents = await context.PvpMatchEvents.AsNoTracking()
                .Where(x => x.MatchId == rankedMatchId &&
                            (x.EventType == "match.forfeited" ||
                             x.EventType == "match.finished"))
                .OrderBy(x => x.Sequence)
                .ToListAsync();
            Assert.Equal(
                ["match.forfeited", "match.finished"],
                terminalEvents.Select(x => x.EventType).ToArray());
            Assert.Equal(
                2,
                terminalEvents.Select(x => x.Sequence).Distinct().Count());
            Assert.All(
                terminalEvents,
                x => Assert.Contains("+07:00", x.PayloadJson, StringComparison.Ordinal));

            // Same quitter retry is idempotent; the opponent cannot overwrite it.
            var retry = await service.ForfeitMatchAsync(users[0], rankedMatchId);
            Assert.Equal(-16, retry.MmrDelta);
            Assert.Single(await context.PvpMatchRewardEntitlements.AsNoTracking()
                .Where(x => x.MatchId == rankedMatchId)
                .ToListAsync());
            Assert.Equal(2, await context.PvpMatchEvents.AsNoTracking()
                .CountAsync(x => x.MatchId == rankedMatchId &&
                                 (x.EventType == "match.forfeited" ||
                                  x.EventType == "match.finished")));
            await Assert.ThrowsAsync<ConflictException>(
                () => service.ForfeitMatchAsync(users[1], rankedMatchId));
            await Assert.ThrowsAsync<NotFoundException>(
                () => service.ClaimRewardAsync(users[0], rankedMatchId));

            // Friendly running match: no MMR delta, but the human winner gets
            // the snapshotted win reward.
            context.Friendships.Add(new Friendship
            {
                UserLowId = Min(users[2], users[3]),
                UserHighId = Max(users[2], users[3]),
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            var invite = await service.CreateInviteAsync(
                users[2],
                new CreatePvpSprintInviteRequest { TargetUserId = users[3] });
            var accepted = await service.RespondInviteAsync(
                users[3],
                invite.InviteId,
                new RespondPvpSprintInviteRequest { Accept = true });
            var friendlyMatchId = Assert.IsType<Guid>(accepted.MatchId);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.pvp_matches
                SET status_code='running',
                    started_at=SYSUTCDATETIME(),
                    ended_at=DATEADD(second,30,SYSUTCDATETIME())
                WHERE match_id={friendlyMatchId};
                """);
            context.ChangeTracker.Clear();

            var friendlyResult = await service.ForfeitMatchAsync(users[2], friendlyMatchId);
            Assert.Equal(0, friendlyResult.MmrDelta);
            Assert.Equal(1000, friendlyResult.MmrAfter);
            var friendlyWinner = await service.GetResultAsync(users[3], friendlyMatchId);
            Assert.Equal(0, friendlyWinner.MmrDelta);
            Assert.True(friendlyWinner.CanClaimReward);

            // Human/bot ranked match: the human loses Elo, but the bot receives
            // neither a profile update nor a reward entitlement.
            var botId = Guid.NewGuid();
            context.PvpBotProfiles.Add(new PvpBotProfile
            {
                BotProfileId = botId,
                DisplayName = "Forfeit Bot",
                Mmr = 1000,
                StepsPerSecond = 2,
                SpiritAffinityCode = "sprout",
                PetStageNo = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            await service.JoinMatchmakingAsync(users[4], new JoinPvpMatchmakingRequest());
            await AgeQueueAsync(context, users[4]);
            await service.ProcessDueWorkAsync();
            var botStatus = await service.GetMatchmakingStatusAsync(users[4]);
            var botMatchId = Assert.IsType<Guid>(botStatus.MatchId);
            var botForfeit = await service.ForfeitMatchAsync(users[4], botMatchId);
            Assert.InRange(botForfeit.MmrDelta, -2, 0);
            Assert.Null(botForfeit.WinnerUserId);
            Assert.False(await context.PvpMatchRewardEntitlements.AsNoTracking()
                .AnyAsync(x => x.MatchId == botMatchId));
            Assert.Equal(1000, await context.PvpBotProfiles.AsNoTracking()
                .Where(x => x.BotProfileId == botId)
                .Select(x => x.Mmr)
                .SingleAsync());
            Assert.Equal(0, await context.PvpPlayerProfiles.AsNoTracking()
                .Where(x => x.UserId == users[4])
                .Select(x => x.ConsecutiveValidRankedLosses)
                .SingleAsync());

            // Two different users forfeiting concurrently: the match lock lets
            // exactly one commit and the other receives a conflict.
            await service.JoinMatchmakingAsync(users[5], new JoinPvpMatchmakingRequest());
            var concurrentAssigned = await service.JoinMatchmakingAsync(
                users[6],
                new JoinPvpMatchmakingRequest());
            var concurrentMatchId = Assert.IsType<Guid>(concurrentAssigned.MatchId);
            context.ChangeTracker.Clear();

            async Task<Exception?> TryForfeitAsync(Guid userId)
            {
                try
                {
                    await using var concurrentContext = scope.CreateContext();
                    await CreateService(concurrentContext, presenceTracker)
                        .ForfeitMatchAsync(userId, concurrentMatchId);
                    return null;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            }

            var concurrentResults = await Task.WhenAll(
                TryForfeitAsync(users[5]),
                TryForfeitAsync(users[6]));
            Assert.Single(concurrentResults, x => x is null);
            Assert.Single(concurrentResults, x => x is ConflictException);
            Assert.Equal(1, await context.PvpMatchEvents.AsNoTracking()
                .CountAsync(x => x.MatchId == concurrentMatchId &&
                                 x.EventType == "match.forfeited"));
            Assert.Equal(1, await context.PvpMatchRewardEntitlements.AsNoTracking()
                .CountAsync(x => x.MatchId == concurrentMatchId));
            Assert.Equal(2000, await context.PvpPlayerProfiles.AsNoTracking()
                .Where(x => x.UserId == users[5] || x.UserId == users[6])
                .SumAsync(x => x.Mmr));

            // Non-playable terminal phases are rejected before settlement.
            await service.JoinMatchmakingAsync(users[7], new JoinPvpMatchmakingRequest());
            var nonPlayableAssigned = await service.JoinMatchmakingAsync(
                users[8],
                new JoinPvpMatchmakingRequest());
            var nonPlayableMatchId = Assert.IsType<Guid>(nonPlayableAssigned.MatchId);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.pvp_matches SET status_code='settling' WHERE match_id={nonPlayableMatchId}");
            context.ChangeTracker.Clear();
            await Assert.ThrowsAsync<ConflictException>(
                () => service.ForfeitMatchAsync(users[7], nonPlayableMatchId));
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.pvp_matches SET status_code='cancelled' WHERE match_id={nonPlayableMatchId}");
            context.ChangeTracker.Clear();
            await Assert.ThrowsAsync<ConflictException>(
                () => service.ForfeitMatchAsync(users[7], nonPlayableMatchId));
        }
    }

    private static PvpSprintService CreateService(
        WalkamonContext context,
        IPvpPresenceTracker presenceTracker) =>
        new(
            context,
            Options.Create(new PvpRealtimeOptions { Enabled = false }),
            Mock.Of<IValidatedStepService>(),
            NullLogger<PvpSprintService>.Instance,
            presenceTracker: presenceTracker);

    private static UpdatePvpRewardRulesRequest RewardMatrix() =>
        new()
        {
            Rules = new[] { "ranked", "friendly", "event" }
                .SelectMany(type => new[]
                {
                    new PvpRewardRuleRequest
                    {
                        MatchTypeCode = type,
                        ResultCode = "lose",
                        WalletAmount = 10
                    },
                    new PvpRewardRuleRequest
                    {
                        MatchTypeCode = type,
                        ResultCode = "draw",
                        WalletAmount = 20
                    },
                    new PvpRewardRuleRequest
                    {
                        MatchTypeCode = type,
                        ResultCode = "win",
                        WalletAmount = 30
                    }
                })
                .ToList()
        };

    private static Task AgeQueueAsync(WalkamonContext context, Guid userId) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.matchmaking_queue
            SET queued_at=DATEADD(second,-16,SYSUTCDATETIME()),
                bot_fallback_at=DATEADD(second,-1,SYSUTCDATETIME())
            WHERE user_id={userId};
            UPDATE dbo.pvp_player_activities
            SET due_at=DATEADD(second,-1,SYSUTCDATETIME())
            WHERE user_id={userId};
            """);

    private static Guid Min(Guid first, Guid second) =>
        first.CompareTo(second) < 0 ? first : second;

    private static Guid Max(Guid first, Guid second) =>
        first.CompareTo(second) < 0 ? second : first;

    private sealed class PvpDatabaseScope : IAsyncDisposable
    {
        private readonly string _masterConnectionString;

        private PvpDatabaseScope(
            string databaseName,
            string masterConnectionString,
            string connectionString)
        {
            DatabaseName = databaseName;
            _masterConnectionString = masterConnectionString;
            ConnectionString = connectionString;
        }

        public string DatabaseName { get; }
        public string ConnectionString { get; }

        public static async Task<PvpDatabaseScope> CreateAsync()
        {
            var databaseName = $"WalkamonPvpForfeit_{Guid.NewGuid():N}";
            const string master =
                "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true";
            var database =
                $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
            var freshSql = await File.ReadAllTextAsync(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Sql",
                    "WalkamonFreshSchema.sql"));
            freshSql = freshSql
                .Replace(
                    "CREATE DATABASE Walkamon;",
                    $"CREATE DATABASE [{databaseName}];",
                    StringComparison.Ordinal)
                .Replace(
                    "USE Walkamon;",
                    $"USE [{databaseName}];",
                    StringComparison.Ordinal);

            await using var connection = new SqlConnection(master);
            await connection.OpenAsync();
            await ExecuteBatchesAsync(connection, freshSql);
            return new PvpDatabaseScope(databaseName, master, database);
        }

        public WalkamonContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<WalkamonContext>()
                .UseSqlServer(
                    ConnectionString,
                    sql => sql.EnableRetryOnFailure())
                .Options;
            return new WalkamonContext(options, new HttpContextAccessor());
        }

        public async Task SeedUsersAsync(IEnumerable<Guid> userIds)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            var index = 0;
            foreach (var userId in userIds)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    DECLARE @role_id INT=(
                        SELECT TOP(1) role_id
                        FROM dbo.roles
                        WHERE role_code='0'
                    );
                    INSERT dbo.users(
                        user_id, role_id, email, normalized_email,
                        email_confirmed, status_code
                    )
                    VALUES(
                        @user_id, @role_id, @email, @normalized_email,
                        1, 'active'
                    );
                    INSERT dbo.user_profiles(user_id, username)
                    VALUES(@user_id, @username);
                    INSERT dbo.wallets(user_id, balance)
                    VALUES(@user_id, 0);
                    """;
                command.Parameters.AddWithValue("@user_id", userId);
                command.Parameters.AddWithValue(
                    "@email",
                    $"pvp-forfeit-{index}@walkamon.test");
                command.Parameters.AddWithValue(
                    "@normalized_email",
                    $"PVP-FORFEIT-{index}@WALKAMON.TEST");
                command.Parameters.AddWithValue(
                    "@username",
                    $"pvp_forfeit_{index}");
                await command.ExecuteNonQueryAsync();
                index++;
            }
        }

        public async Task SeedPetAsync(Guid userId)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @pet_id UNIQUEIDENTIFIER=NEWID();
                DECLARE @stage1 UNIQUEIDENTIFIER=NEWID();
                DECLARE @stage2 UNIQUEIDENTIFIER=NEWID();
                DECLARE @stage3 UNIQUEIDENTIFIER=NEWID();
                INSERT dbo.pets(
                    pet_id, pet_name, pvp_affinity_code, life_force,
                    energy, bond, exp
                )
                VALUES(
                    @pet_id, N'Warm Pet', 'warm_sun', 100, 100, 100, 100
                );
                INSERT dbo.pet_stages(
                    stage_id, pet_id, stage_no, stage_name, required_level
                )
                VALUES
                    (@stage1, @pet_id, 1, N'Stage 1', 1),
                    (@stage2, @pet_id, 2, N'Stage 2', 10),
                    (@stage3, @pet_id, 3, N'Stage 3', 20);
                INSERT dbo.user_pets(user_id, pet_id, level, pet_name)
                VALUES(@user_id, @pet_id, 12, N'Warm Test Spirit');
                INSERT dbo.pet_evolution_history(
                    evolution_id, user_id, stage_id, level, evolved_at
                )
                VALUES(NEWID(), @user_id, @stage2, 12, SYSUTCDATETIME());
                """;
            command.Parameters.AddWithValue("@user_id", userId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task EvolvePetAfterSnapshotAsync(Guid userId)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @stage3 UNIQUEIDENTIFIER=(
                    SELECT ps.stage_id
                    FROM dbo.user_pets up
                    INNER JOIN dbo.pet_stages ps ON ps.pet_id=up.pet_id
                    WHERE up.user_id=@user_id AND ps.stage_no=3
                );
                UPDATE dbo.user_pets
                SET pet_name=N'Renamed After Match', level=20
                WHERE user_id=@user_id;
                INSERT dbo.pet_evolution_history(
                    evolution_id, user_id, stage_id, level, evolved_at
                )
                VALUES(NEWID(), @user_id, @stage3, 20, SYSUTCDATETIME());
                """;
            command.Parameters.AddWithValue("@user_id", userId);
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(_masterConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF DB_ID(N'{DatabaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{DatabaseName}]
                        SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{DatabaseName}];
                END
                """;
            await command.ExecuteNonQueryAsync();
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
}
