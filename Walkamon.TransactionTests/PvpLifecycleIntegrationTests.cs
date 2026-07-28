using System.Text.RegularExpressions;
using System.Text.Json;
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

[Trait("UC", "UC-67")]
[Trait("UC", "UC-68")]
[Trait("UC", "UC-69")]
[Trait("UC", "UC-71")]
public sealed class PvpLifecycleIntegrationTests
{
    [Fact]
    public async Task Matchmaking_Lifecycle_Snapshot_Claim_BotFallback_AndInviteRecovery_Work()
    {
        var databaseName = $"WalkamonPvpLifecycle_{Guid.NewGuid():N}";
        var master = "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true";
        var database = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
        var freshSql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", "WalkamonFreshSchema.sql"));
        freshSql = freshSql
            .Replace("CREATE DATABASE Walkamon;", $"CREATE DATABASE [{databaseName}];", StringComparison.Ordinal)
            .Replace("USE Walkamon;", $"USE [{databaseName}];", StringComparison.Ordinal);

        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var botUserId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();

        try
        {
            await using (var connection = new SqlConnection(master))
            {
                await connection.OpenAsync();
                await ExecuteBatchesAsync(connection, freshSql);
            }

            await SeedUsersAsync(database, firstUserId, secondUserId, botUserId, inviterId, inviteeId);
            await using var context = CreateContext(database);
            var service = CreateService(context);
            await service.UpdateRewardRulesAsync(RewardMatrix(10, 20, 30));
            var vietnamToday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            context.DailySteps.AddRange(
                new DailyStep
                {
                    UserId = firstUserId,
                    StepDate = vietnamToday,
                    StepCount = 10000,
                    EligibleStepCount = 10000,
                    UpdatedAt = DateTime.UtcNow
                },
                new DailyStep
                {
                    UserId = secondUserId,
                    StepDate = vietnamToday,
                    StepCount = 0,
                    EligibleStepCount = 0,
                    UpdatedAt = DateTime.UtcNow
                });
            await context.SaveChangesAsync();

            var waiting = await service.JoinMatchmakingAsync(firstUserId, new JoinPvpMatchmakingRequest());
            Assert.Equal("waiting", waiting.StatusCode);
            Assert.Null(waiting.MatchId);

            var assigned = await service.JoinMatchmakingAsync(secondUserId, new JoinPvpMatchmakingRequest());
            var matchId = Assert.IsType<Guid>(assigned.MatchId);
            Assert.Equal("countdown", assigned.StatusCode);
            Assert.Equal(3, await context.PvpMatchRewardSnapshots.CountAsync(x => x.MatchId == matchId));
            var waitingForReady = await service.GetMatchAsync(firstUserId, matchId);
            Assert.Null(waitingForReady.CountdownStartsAt);
            Assert.Null(waitingForReady.CountdownEndsAt);
            Assert.All(waitingForReady.Participants, participant => Assert.False(participant.IsReady));

            var firstReady = await service.ReadyMatchAsync(firstUserId, matchId);
            Assert.False(firstReady.AllReady);
            Assert.Null(firstReady.CountdownStartsAt);
            Assert.Null(firstReady.CountdownEndsAt);

            var secondReady = await service.ReadyMatchAsync(secondUserId, matchId);
            Assert.True(secondReady.AllReady);
            Assert.NotNull(secondReady.CountdownStartsAt);
            Assert.NotNull(secondReady.CountdownEndsAt);
            Assert.Equal(
                TimeSpan.FromSeconds(5),
                secondReady.CountdownEndsAt!.Value - secondReady.CountdownStartsAt!.Value);
            Assert.True(secondReady.CountdownStartsAt.Value >= secondReady.ServerTime.AddSeconds(2));

            var duplicateReady = await service.ReadyMatchAsync(secondUserId, matchId);
            Assert.True(duplicateReady.AllReady);
            Assert.Equal(secondReady.CountdownStartsAt, duplicateReady.CountdownStartsAt);
            Assert.Equal(secondReady.CountdownEndsAt, duplicateReady.CountdownEndsAt);
            Assert.Single(await context.PvpMatchEvents.AsNoTracking()
                .Where(x => x.MatchId == matchId && x.EventType == "match.countdown.started")
                .ToListAsync());
            var countdownEventJson = await context.PvpMatchEvents.AsNoTracking()
                .Where(x => x.MatchId == matchId && x.EventType == "match.countdown.started")
                .Select(x => x.PayloadJson)
                .SingleAsync();
            using (var countdownEvent = JsonDocument.Parse(countdownEventJson))
            {
                var details = countdownEvent.RootElement.GetProperty("details");
                Assert.EndsWith("+07:00", details.GetProperty("countdownStartsAt").GetString());
                Assert.EndsWith("+07:00", details.GetProperty("countdownEndsAt").GetString());
            }

            // Admin changes apply only to future matches. This match must settle
            // from the immutable reward snapshot created above.
            await service.UpdateRewardRulesAsync(RewardMatrix(700, 800, 900));

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.pvp_matches SET countdown_ends_at=DATEADD(second,-1,SYSUTCDATETIME()) WHERE match_id={matchId}");
            await service.ProcessDueWorkAsync();
            Assert.Equal("running", await MatchStatusAsync(context, matchId));
            var powerSnapshots = await context.PvpMatchPlayers.AsNoTracking()
                .Where(x => x.MatchId == matchId)
                .ToDictionaryAsync(x => x.UserId!.Value);
            Assert.Equal(10000, powerSnapshots[firstUserId].DailyEligibleStepsSnapshot);
            Assert.Equal(2500, powerSnapshots[firstUserId].BasePaceMilliStepsPerSecond);
            Assert.Equal(0, powerSnapshots[secondUserId].DailyEligibleStepsSnapshot);
            Assert.Equal(1000, powerSnapshots[secondUserId].BasePaceMilliStepsPerSecond);

            await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.pvp_matches
                SET created_at=DATEADD(second,-32,SYSUTCDATETIME()),
                    started_at=DATEADD(second,-31,SYSUTCDATETIME()),
                    ended_at=DATEADD(second,-1,SYSUTCDATETIME())
                WHERE match_id={matchId};
                """);
            await service.ProcessDueWorkAsync();
            Assert.Equal("finished", await MatchStatusAsync(context, matchId));

            var players = await context.PvpMatchPlayers.AsNoTracking()
                .Where(x => x.MatchId == matchId)
                .OrderByDescending(x => x.DistanceUnits)
                .ToListAsync();
            Assert.Equal("win", players[0].ResultCode);
            Assert.Equal(16, players[0].MmrDelta);
            Assert.Equal("lose", players[1].ResultCode);
            Assert.Equal(-16, players[1].MmrDelta);

            var entitlements = await context.PvpMatchRewardEntitlements.AsNoTracking()
                .Where(x => x.MatchId == matchId)
                .ToDictionaryAsync(x => x.UserId);
            Assert.Equal(30, entitlements[firstUserId].WalletAmount);
            Assert.Equal(10, entitlements[secondUserId].WalletAmount);

            var result = await service.GetResultAsync(firstUserId, matchId);
            Assert.True(result.CanClaimReward);
            Assert.Equal(1016, result.MmrAfter);
            var history = await service.GetHistoryAsync(firstUserId, 1, 20, null, null, null, null, false);
            Assert.Single(history.Items);

            var claim = await service.ClaimRewardAsync(firstUserId, matchId);
            Assert.Equal(30, claim.WalletReward);
            Assert.Equal(30, claim.WalletBalance);
            await Assert.ThrowsAsync<ConflictException>(() => service.ClaimRewardAsync(firstUserId, matchId));

            var eventSequences = await context.PvpMatchEvents.AsNoTracking()
                .Where(x => x.MatchId == matchId)
                .Select(x => x.Sequence)
                .ToListAsync();
            Assert.Equal(eventSequences.Count, eventSequences.Distinct().Count());
            Assert.Equal(
                new long[] { 1, 2, 3, 4, 5, 6 },
                eventSequences.Order().ToArray());

            // At 15 seconds the queue falls back only when an active bot exists.
            await service.JoinMatchmakingAsync(botUserId, new JoinPvpMatchmakingRequest());
            await AgeQueueAsync(context, botUserId);
            await service.ProcessDueWorkAsync();
            Assert.Equal("waiting", (await service.GetMatchmakingStatusAsync(botUserId)).StatusCode);

            context.PvpBotProfiles.Add(new PvpBotProfile
            {
                BotProfileId = Guid.NewGuid(),
                DisplayName = "Lifecycle Bot",
                Mmr = 1000,
                StepsPerSecond = 2,
                SpiritAffinityCode = "sprout",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            await service.ProcessDueWorkAsync();
            var botStatus = await service.GetMatchmakingStatusAsync(botUserId);
            var botMatchId = Assert.IsType<Guid>(botStatus.MatchId);
            Assert.Equal("countdown", botStatus.StatusCode);
            var botParticipants = await context.PvpMatchPlayers.AsNoTracking()
                .Where(x => x.MatchId == botMatchId)
                .ToListAsync();
            var botParticipant = Assert.Single(botParticipants, x => x.ParticipantTypeCode == "bot");
            Assert.Null(botParticipant.UserId);
            Assert.NotNull(botParticipant.BotProfileId);
            Assert.True(botParticipant.IsReady);
            Assert.False(Assert.Single(botParticipants, x => x.ParticipantTypeCode == "user").IsReady);
            var botReady = await service.ReadyMatchAsync(botUserId, botMatchId);
            Assert.True(botReady.AllReady);
            Assert.Equal(
                TimeSpan.FromSeconds(5),
                botReady.CountdownEndsAt!.Value - botReady.CountdownStartsAt!.Value);

            // Accepted friend invite waits for both users. If nobody becomes
            // ready before the activity deadline, the match is cancelled and
            // both activity locks are released.
            var low = inviterId.CompareTo(inviteeId) < 0 ? inviterId : inviteeId;
            var high = inviterId.CompareTo(inviteeId) < 0 ? inviteeId : inviterId;
            context.Friendships.Add(new Friendship
            {
                UserLowId = low,
                UserHighId = high,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            var invite = await service.CreateInviteAsync(inviterId, new CreatePvpSprintInviteRequest
            {
                TargetUserId = inviteeId
            });
            var acceptedInvite = await service.RespondInviteAsync(
                inviteeId,
                invite.InviteId,
                new RespondPvpSprintInviteRequest { Accept = true });
            var inviteMatchId = Assert.IsType<Guid>(acceptedInvite.MatchId);
            var acceptedRetry = await service.RespondInviteAsync(
                inviteeId,
                invite.InviteId,
                new RespondPvpSprintInviteRequest { Accept = true });
            Assert.Equal(inviteMatchId, acceptedRetry.MatchId);
            Assert.Equal(1, await context.PvpMatches.CountAsync(
                x => x.MatchId == inviteMatchId));
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.pvp_player_activities SET due_at=DATEADD(second,-1,SYSUTCDATETIME()) WHERE activity_id={inviteMatchId}");
            await service.ProcessDueWorkAsync();
            var recovered = await context.PvpMatches.AsNoTracking().SingleAsync(x => x.MatchId == inviteMatchId);
            Assert.Equal("cancelled", recovered.StatusCode);
            Assert.Equal("ready_timeout", recovered.CancelReason);
            Assert.False(await context.PvpPlayerActivities.AnyAsync(
                x => x.UserId == inviterId || x.UserId == inviteeId));
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

    private static PvpSprintService CreateService(WalkamonContext context) =>
        new(
            context,
            Options.Create(new PvpRealtimeOptions { Enabled = false }),
            Mock.Of<IValidatedStepService>(),
            NullLogger<PvpSprintService>.Instance);

    private static WalkamonContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<WalkamonContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;
        return new WalkamonContext(options, new HttpContextAccessor());
    }

    private static UpdatePvpRewardRulesRequest RewardMatrix(int lose, int draw, int win) =>
        new()
        {
            Rules = new[] { "ranked", "friendly", "event" }
                .SelectMany(type => new[]
                {
                    new PvpRewardRuleRequest { MatchTypeCode = type, ResultCode = "lose", WalletAmount = lose },
                    new PvpRewardRuleRequest { MatchTypeCode = type, ResultCode = "draw", WalletAmount = draw },
                    new PvpRewardRuleRequest { MatchTypeCode = type, ResultCode = "win", WalletAmount = win }
                })
                .ToList()
        };

    private static async Task<string> MatchStatusAsync(WalkamonContext context, Guid matchId) =>
        await context.PvpMatches.AsNoTracking()
            .Where(x => x.MatchId == matchId)
            .Select(x => x.StatusCode)
            .SingleAsync();

    private static Task AgeQueueAsync(WalkamonContext context, Guid userId) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.matchmaking_queue
            SET queued_at=DATEADD(second,-16,SYSUTCDATETIME())
            WHERE user_id={userId};
            UPDATE dbo.pvp_player_activities
            SET due_at=DATEADD(second,-1,SYSUTCDATETIME())
            WHERE user_id={userId};
            """);

    private static async Task SeedUsersAsync(string connectionString, params Guid[] userIds)
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
            command.Parameters.AddWithValue("@email", $"pvp-lifecycle-{index}@walkamon.test");
            command.Parameters.AddWithValue("@normalized_email", $"PVP-LIFECYCLE-{index}@WALKAMON.TEST");
            command.Parameters.AddWithValue("@username", $"pvp_lifecycle_{index}");
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task ExecuteBatchesAsync(SqlConnection connection, string script)
    {
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*;?\s*$",
                     RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 60;
            await command.ExecuteNonQueryAsync();
        }
    }
}
