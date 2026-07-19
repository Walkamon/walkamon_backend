using System.Text.RegularExpressions;
using DAL.Data;
using DAL.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class AuthRegistrationCleanupIntegrationTests : IAsyncLifetime
{
    private static readonly Guid ExpiredUserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid CurrentOtpUserId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid ExpiredRequestCode =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid CurrentRequestCode =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly string _databaseName =
        $"WalkamonAuthCleanupTests_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Integrated Security=true;TrustServerCertificate=true";

    public async Task InitializeAsync()
    {
        var schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Sql",
            "WalkamonFreshSchema.sql");
        var schema = await File.ReadAllTextAsync(schemaPath);
        schema = schema
            .Replace(
                "CREATE DATABASE Walkamon;",
                $"CREATE DATABASE [{_databaseName}];",
                StringComparison.Ordinal)
            .Replace(
                "USE Walkamon;",
                $"USE [{_databaseName}];",
                StringComparison.Ordinal);

        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await ExecuteBatchesAsync(connection, schema);

        await SeedPendingRegistrationsAsync();
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

    [Fact]
    public async Task CleanupExpiredPendingRegistrations_CancelsOnlyOldOtp_AndPreservesUserData()
    {
        await using (var context = CreateContext())
        {
            var repository = new UserRepository(context);

            await repository.CleanupExpiredPendingRegistrationsAsync(
                DateTime.UtcNow.AddHours(-24));
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        Assert.Equal(
            "cancelled",
            await GetOtpStatusAsync(connection, ExpiredRequestCode));
        Assert.Equal(
            "pending",
            await GetOtpStatusAsync(connection, CurrentRequestCode));
        Assert.Equal(1, await CountByUserIdAsync(connection, "users", ExpiredUserId));
        Assert.Equal(1, await CountByUserIdAsync(connection, "user_profiles", ExpiredUserId));
        Assert.Equal(1, await CountByUserIdAsync(connection, "pvp_player_profiles", ExpiredUserId));
        Assert.Equal(1, await CountByUserIdAsync(connection, "users", CurrentOtpUserId));
    }

    private const string MasterConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true";

    private WalkamonContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WalkamonContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new WalkamonContext(options, new HttpContextAccessor());
    }

    private async Task SeedPendingRegistrationsAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @roleId INT =
                (SELECT role_id FROM dbo.roles WHERE role_code = '0');

            INSERT dbo.users
                (user_id, role_id, email, normalized_email, email_confirmed,
                 status_code, created_at, updated_at)
            VALUES
                (@expiredUserId, @roleId, N'expired@example.com',
                 N'EXPIRED@EXAMPLE.COM', 0, 'active',
                 DATEADD(HOUR, -72, SYSUTCDATETIME()),
                 DATEADD(HOUR, -72, SYSUTCDATETIME())),
                (@currentOtpUserId, @roleId, N'current@example.com',
                 N'CURRENT@EXAMPLE.COM', 0, 'active',
                 DATEADD(HOUR, -72, SYSUTCDATETIME()),
                 DATEADD(HOUR, -72, SYSUTCDATETIME()));

            INSERT dbo.user_profiles (user_id, username)
            VALUES (@expiredUserId, N'expired-user'),
                   (@currentOtpUserId, N'current-user');

            INSERT dbo.pvp_player_profiles (user_id)
            VALUES (@expiredUserId);

            INSERT dbo.otp_requests
                (user_id, purpose_code, target_value, otp_hash, request_code,
                 expires_at, status_code, created_at, updated_at)
            VALUES
                (@expiredUserId, 'verify_email', N'expired@example.com',
                 HASHBYTES('SHA2_256', '123456'), @expiredRequestCode,
                 DATEADD(HOUR, -47, SYSUTCDATETIME()), 'pending',
                 DATEADD(HOUR, -48, SYSUTCDATETIME()),
                 DATEADD(HOUR, -48, SYSUTCDATETIME())),
                (@currentOtpUserId, 'verify_email', N'current@example.com',
                 HASHBYTES('SHA2_256', '654321'), @currentRequestCode,
                 DATEADD(HOUR, 1, SYSUTCDATETIME()), 'pending',
                 SYSUTCDATETIME(), SYSUTCDATETIME());
            """;
        command.Parameters.AddWithValue("@expiredUserId", ExpiredUserId);
        command.Parameters.AddWithValue("@currentOtpUserId", CurrentOtpUserId);
        command.Parameters.AddWithValue("@expiredRequestCode", ExpiredRequestCode);
        command.Parameters.AddWithValue("@currentRequestCode", CurrentRequestCode);
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
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 60;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<string> GetOtpStatusAsync(
        SqlConnection connection,
        Guid requestCode)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT status_code
            FROM dbo.otp_requests
            WHERE request_code = @requestCode;
            """;
        command.Parameters.AddWithValue("@requestCode", requestCode);

        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> CountByUserIdAsync(
        SqlConnection connection,
        string tableName,
        Guid userId)
    {
        var allowedTables = new HashSet<string>(StringComparer.Ordinal)
        {
            "users",
            "user_profiles",
            "pvp_player_profiles"
        };
        Assert.Contains(tableName, allowedTables);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT COUNT(*)
            FROM dbo.[{tableName}]
            WHERE user_id = @userId;
            """;
        command.Parameters.AddWithValue("@userId", userId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
