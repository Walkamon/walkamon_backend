using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using BLL.Interfaces;
using BLL.Service;
using DAL.Data;
using DAL.Models;
using DAL.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Walkamon.IntegrationTests.Notifications;

[Collection(DailyActivityReminderSqlCollection.Name)]
public sealed class DailyActivityReminderSqlTests(
    DailyActivityReminderSqlFixture fixture)
{
    private static readonly DateTimeOffset Vietnam1800 =
        new(2026, 8, 17, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GenericSchedulerDoesNotBroadcastSingleUserReminderClaims()
    {
        await fixture.DisableExistingUsersAsync();
        var now = Vietnam1800.UtcDateTime;

        await using var context = fixture.CreateContext();
        context.Notifications.AddRange(
            new DAL.Models.Notification
            {
                NotificationId = Guid.NewGuid(),
                NotificationTypeCode = DailyActivityReminderConstants.NotificationTypeCode,
                Title = "single",
                Body = "single",
                TargetAudienceCode = "single_user",
                StatusCode = "scheduled",
                ScheduledAt = now,
                CreatedAt = now,
                UpdatedAt = now
            },
            new DAL.Models.Notification
            {
                NotificationId = Guid.NewGuid(),
                NotificationTypeCode = "news",
                Title = "broadcast",
                Body = "broadcast",
                TargetAudienceCode = "all_users",
                StatusCode = "scheduled",
                ScheduledAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        await context.SaveChangesAsync();

        var due = await new NotificationRepository(context)
            .GetDueScheduledNotificationsAsync(now, 100);

        Assert.Single(due);
        Assert.Equal("all_users", due[0].TargetAudienceCode);
    }

    [Fact]
    public async Task RepeatedWorkerRunsPersistAndSendExactlyOneReminder()
    {
        await fixture.DisableExistingUsersAsync();
        var userId = await fixture.SeedUserAsync(
            new DateOnly(2026, 8, 17),
            rawSteps: 3000,
            authoritativeSteps: 3000);
        var push = new RecordingFcmPushService();
        var clock = new MutableTimeProvider(Vietnam1800);

        await using (var context = fixture.CreateContext())
        {
            var first = await CreateService(context, push, clock).ProcessAsync();
            Assert.Equal(1, first.SentUsers);
        }
        await using (var context = fixture.CreateContext())
        {
            var retry = await CreateService(context, push, clock).ProcessAsync();
            Assert.Equal(1, retry.AlreadySentSkipped);
            Assert.Equal(0, retry.SentUsers);
        }

        Assert.Equal(1, push.SendCount);
        Assert.Equal(1, await fixture.CountRemindersAsync(userId));
        Assert.Equal(1, await fixture.CountUserRemindersAsync(userId));
        Assert.Equal("sent", await fixture.ReadReminderStatusAsync(userId));
    }

    [Fact]
    public async Task ConcurrentWorkersClaimOneDatabaseIdentityAndSendOnce()
    {
        await fixture.DisableExistingUsersAsync();
        var userId = await fixture.SeedUserAsync(
            new DateOnly(2026, 8, 17),
            rawSteps: 3000,
            authoritativeSteps: 3000);
        var push = new RecordingFcmPushService(sendDelay: TimeSpan.FromMilliseconds(100));
        var clock = new MutableTimeProvider(Vietnam1800);

        async Task<DailyActivityReminderRunResult> RunAsync()
        {
            await using var context = fixture.CreateContext();
            return await CreateService(context, push, clock).ProcessAsync();
        }

        var results = await Task.WhenAll(RunAsync(), RunAsync());

        Assert.Equal(1, results.Sum(x => x.SentUsers));
        Assert.Equal(1, push.SendCount);
        Assert.Equal(1, await fixture.CountRemindersAsync(userId));
        Assert.Equal(1, await fixture.CountUserRemindersAsync(userId));
    }

    [Fact]
    public async Task FcmFailureRetriesAfterLeaseWithoutCreatingDuplicateRows()
    {
        await fixture.DisableExistingUsersAsync();
        var userId = await fixture.SeedUserAsync(
            new DateOnly(2026, 8, 17),
            rawSteps: 3000,
            authoritativeSteps: 3000);
        var push = new RecordingFcmPushService(failuresBeforeSuccess: 1);
        var clock = new MutableTimeProvider(Vietnam1800);

        await using (var context = fixture.CreateContext())
        {
            var failed = await CreateService(context, push, clock).ProcessAsync();
            Assert.Equal(1, failed.Failures);
        }
        await using (var context = fixture.CreateContext())
        {
            var deferred = await CreateService(context, push, clock).ProcessAsync();
            Assert.Equal(1, deferred.RetryDeferred);
            Assert.Equal(1, push.SendCount);
        }

        clock.Advance(TimeSpan.FromMinutes(6));
        await using (var context = fixture.CreateContext())
        {
            var delivered = await CreateService(context, push, clock).ProcessAsync();
            Assert.Equal(1, delivered.SentUsers);
        }

        Assert.Equal(2, push.SendCount);
        Assert.Equal(1, await fixture.CountRemindersAsync(userId));
        Assert.Equal(1, await fixture.CountUserRemindersAsync(userId));
        Assert.Equal("sent", await fixture.ReadReminderStatusAsync(userId));
    }

    [Fact]
    public async Task ReminderUsesEligibleStepCountInsteadOfRawOrPendingEvidence()
    {
        await fixture.DisableExistingUsersAsync();
        var userId = await fixture.SeedUserAsync(
            new DateOnly(2026, 8, 17),
            rawSteps: 10000,
            authoritativeSteps: 3000,
            languageCode: "vi-VN");
        var push = new RecordingFcmPushService();

        await using var context = fixture.CreateContext();
        var result = await CreateService(
            context,
            push,
            new MutableTimeProvider(Vietnam1800)).ProcessAsync();

        Assert.Equal(1, result.SentUsers);
        var sent = Assert.Single(push.Messages);
        Assert.Equal(userId, sent.UserId);
        Assert.Contains("3.000 bước", sent.Body, StringComparison.Ordinal);
        Assert.Contains("4.000 bước", sent.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("10.000", sent.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GoalReachedDisabledAndMissingTokenAreSkippedWithReasons()
    {
        await fixture.DisableExistingUsersAsync();
        await fixture.SeedUserAsync(
            new DateOnly(2026, 8, 17),
            rawSteps: 7000,
            authoritativeSteps: 7000);
        await fixture.SeedUserAsync(
            new DateOnly(2026, 8, 17),
            rawSteps: 3000,
            authoritativeSteps: 3000,
            notificationsEnabled: false);
        await fixture.SeedUserAsync(
            new DateOnly(2026, 8, 17),
            rawSteps: 3000,
            authoritativeSteps: 3000,
            hasToken: false);
        var push = new RecordingFcmPushService();

        await using var context = fixture.CreateContext();
        var result = await CreateService(
            context,
            push,
            new MutableTimeProvider(Vietnam1800)).ProcessAsync();

        Assert.Equal(1, result.GoalReachedSkipped);
        Assert.Equal(1, result.NotificationDisabledSkipped);
        Assert.Equal(1, result.MissingTokenSkipped);
        Assert.Equal(0, result.SentUsers);
        Assert.Equal(0, push.SendCount);
    }

    [Fact]
    public async Task CustomGoalAndLocalDateCreateIndependentDailyReminderIdentity()
    {
        await fixture.DisableExistingUsersAsync();
        var userId = await fixture.SeedUserAsync(
            new DateOnly(2026, 8, 17),
            rawSteps: 7500,
            authoritativeSteps: 7500,
            customGoal: 10000,
            languageCode: "en-US");
        var push = new RecordingFcmPushService();
        var clock = new MutableTimeProvider(Vietnam1800);

        await using (var context = fixture.CreateContext())
        {
            await CreateService(context, push, clock).ProcessAsync();
        }
        clock.Advance(TimeSpan.FromDays(1));
        await using (var context = fixture.CreateContext())
        {
            await CreateService(context, push, clock).ProcessAsync();
        }

        Assert.Equal(2, push.SendCount);
        Assert.Equal(2, await fixture.CountRemindersAsync(userId));
        Assert.Contains(
            push.Messages,
            x => x.Body.Contains("2,500 more", StringComparison.Ordinal));
    }

    private static DailyActivityReminderService CreateService(
        WalkamonContext context,
        IFcmPushService push,
        TimeProvider clock) =>
        new(
            context,
            push,
            clock,
            NullLogger<DailyActivityReminderService>.Instance);
}

[CollectionDefinition(Name)]
public sealed class DailyActivityReminderSqlCollection :
    ICollectionFixture<DailyActivityReminderSqlFixture>
{
    public const string Name = "DailyActivityReminderSql";
}

public sealed class DailyActivityReminderSqlFixture : IAsyncLifetime
{
    private static readonly string MasterConnectionString =
        Environment.GetEnvironmentVariable("WALKAMON_TEST_SQL_MASTER")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true";
    private readonly string _databaseName =
        $"WalkamonDailyReminder_{Guid.NewGuid():N}";

    private string ConnectionString
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(MasterConnectionString)
            {
                InitialCatalog = _databaseName
            };
            return builder.ConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        var schema = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Sql", "WalkamonFreshSchema.sql"));
        schema = schema
            .Replace(
                "CREATE DATABASE Walkamon;",
                $"CREATE DATABASE [{_databaseName}];",
                StringComparison.Ordinal)
            .Replace(
                "USE Walkamon;",
                $"USE [{_databaseName}];",
                StringComparison.Ordinal);
        await using var master = new SqlConnection(MasterConnectionString);
        await master.OpenAsync();
        await ExecuteBatchesAsync(master, schema);

        await ExecuteAsync(
            """
            UPDATE dbo.system_settings
            SET setting_value = 'true', updated_at = SYSUTCDATETIME()
            WHERE setting_key = 'daily_activity_reminder_enabled';
            """);
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{_databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_databaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
    }

    public WalkamonContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WalkamonContext>()
            .UseSqlServer(
                ConnectionString,
                sql => sql.EnableRetryOnFailure(
                    5,
                    TimeSpan.FromMilliseconds(100),
                    null))
            .Options;
        return new WalkamonContext(options, new HttpContextAccessor());
    }

    public Task DisableExistingUsersAsync() => ExecuteAsync(
        """
        UPDATE dbo.users
        SET status_code = 'disabled', updated_at = SYSUTCDATETIME()
        WHERE email LIKE 'reminder-%@local.invalid';
        """);

    public async Task<Guid> SeedUserAsync(
        DateOnly localDate,
        int rawSteps,
        int authoritativeSteps,
        int? customGoal = null,
        bool notificationsEnabled = true,
        bool hasToken = true,
        string languageCode = "vi-VN",
        string timeZoneId = "Asia/Ho_Chi_Minh")
    {
        var userId = Guid.NewGuid();
        await ExecuteAsync(
            """
            DECLARE @roleId INT =
                (SELECT TOP (1) role_id FROM dbo.roles WHERE role_name = N'User');

            INSERT dbo.users
                (user_id, role_id, email, normalized_email, password_hash,
                 email_confirmed, status_code, created_at, updated_at)
            VALUES
                (@userId, @roleId, @email, UPPER(@email), 'REMINDER-TEST-NOT-A-LOGIN',
                 1, 'active', SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT dbo.user_profiles
                (user_id, username, language_code, theme_code, time_zone_id,
                 show_activity_stats, notifications_enabled)
            VALUES
                (@userId, @username, @languageCode, 'light', @timeZoneId,
                 1, @notificationsEnabled);

            INSERT dbo.daily_steps
                (user_id, step_date, step_count, eligible_step_count, updated_at)
            VALUES
                (@userId, @localDate, @rawSteps, @authoritativeSteps, SYSUTCDATETIME());

            IF @hasToken = 1
                INSERT dbo.device_tokens(user_id, fcm_token, is_active, updated_at)
                VALUES (@userId, @token, 1, SYSUTCDATETIME());

            IF @customGoal IS NOT NULL
                INSERT dbo.step_goals(user_id, effective_from, target_steps)
                VALUES (@userId, @localDate, @customGoal);
            """,
            new SqlParameter("@userId", userId),
            new SqlParameter("@email", $"reminder-{userId:N}@local.invalid"),
            new SqlParameter("@username", $"reminder-{userId:N}"[..30]),
            new SqlParameter("@languageCode", languageCode),
            new SqlParameter("@timeZoneId", timeZoneId),
            new SqlParameter("@notificationsEnabled", notificationsEnabled),
            new SqlParameter("@localDate", localDate.ToDateTime(TimeOnly.MinValue)),
            new SqlParameter("@rawSteps", rawSteps),
            new SqlParameter("@authoritativeSteps", authoritativeSteps),
            new SqlParameter("@hasToken", hasToken),
            new SqlParameter("@token", $"test-token-{userId:N}"),
            new SqlParameter("@customGoal", (object?)customGoal ?? DBNull.Value));
        return userId;
    }

    public Task<int> CountRemindersAsync(Guid userId) => ScalarAsync<int>(
        """
        SELECT COUNT(*)
        FROM dbo.notifications n
        JOIN dbo.user_notifications un ON un.notification_id = n.notification_id
        WHERE un.user_id = @userId
          AND n.notification_type_code = 'daily_step_goal_reminder'
        """,
        new SqlParameter("@userId", userId));

    public Task<int> CountUserRemindersAsync(Guid userId) => ScalarAsync<int>(
        """
        SELECT COUNT(*)
        FROM dbo.user_notifications un
        JOIN dbo.notifications n ON n.notification_id = un.notification_id
        WHERE un.user_id = @userId
          AND n.notification_type_code = 'daily_step_goal_reminder'
        """,
        new SqlParameter("@userId", userId));

    public Task<string> ReadReminderStatusAsync(Guid userId) => ScalarAsync<string>(
        """
        SELECT TOP (1) n.status_code
        FROM dbo.notifications n
        JOIN dbo.user_notifications un ON un.notification_id = n.notification_id
        WHERE un.user_id = @userId
          AND n.notification_type_code = 'daily_step_goal_reminder'
        ORDER BY n.created_at DESC
        """,
        new SqlParameter("@userId", userId));

    private async Task ExecuteAsync(string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(
        string sql,
        params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(value!, typeof(T));
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
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync();
        }
    }
}

internal sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
}

internal sealed class RecordingFcmPushService : IFcmPushService
{
    private readonly TimeSpan _sendDelay;
    private int _failuresRemaining;
    private int _sendCount;
    private readonly ConcurrentQueue<RecordedPush> _messages = new();

    public RecordingFcmPushService(
        int failuresBeforeSuccess = 0,
        TimeSpan? sendDelay = null)
    {
        _failuresRemaining = failuresBeforeSuccess;
        _sendDelay = sendDelay ?? TimeSpan.Zero;
    }

    public bool IsConfigured => true;
    public int SendCount => Volatile.Read(ref _sendCount);
    public IReadOnlyCollection<RecordedPush> Messages => _messages.ToArray();

    public async Task SendAsync(
        DeviceToken deviceToken,
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _sendCount);
        if (_sendDelay > TimeSpan.Zero)
        {
            await Task.Delay(_sendDelay, cancellationToken);
        }

        if (Interlocked.Decrement(ref _failuresRemaining) >= 0)
        {
            throw new InvalidOperationException("Synthetic FCM network failure.");
        }

        _messages.Enqueue(new RecordedPush(
            deviceToken.UserId,
            notification.NotificationId,
            notification.Title,
            notification.Body));
    }
}

internal sealed record RecordedPush(
    Guid UserId,
    Guid NotificationId,
    string Title,
    string Body);
