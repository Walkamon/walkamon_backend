using DAL.Data;
using DAL.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Xunit;

namespace Walkamon.TransactionTests;

public sealed class RetryableTransactionIntegrationTests : IAsyncLifetime
{
    private readonly string _databaseName =
        $"WalkamonTransactionTests_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Integrated Security=true;TrustServerCertificate=true";

    public async Task InitializeAsync()
    {
        await using var connection = new SqlConnection(
            "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{_databaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var connection = new SqlConnection(
            "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true");
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
    public async Task ExecuteInTransactionAsync_Commits_WhenRetryIsEnabled()
    {
        await using var context = CreateContext();

        await context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            () => context.Database.ExecuteSqlRawAsync(
                "CREATE TABLE dbo.retryable_commit_test (id int NOT NULL PRIMARY KEY)"));

        Assert.True(await TableExistsAsync("retryable_commit_test"));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBack_WhenOperationFails()
    {
        await using var context = CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.ExecuteInTransactionAsync(
                IsolationLevel.Serializable,
                async () =>
                {
                    await context.Database.ExecuteSqlRawAsync(
                        "CREATE TABLE dbo.retryable_rollback_test (id int NOT NULL PRIMARY KEY)");
                    throw new InvalidOperationException("Expected test failure");
                }));

        Assert.False(await TableExistsAsync("retryable_rollback_test"));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_Reuses_AmbientRetryableTransaction()
    {
        await using var context = CreateContext();
        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            await context.ExecuteInTransactionAsync(
                IsolationLevel.Serializable,
                () => context.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE dbo.retryable_ambient_test (id int NOT NULL PRIMARY KEY)"));

            await transaction.CommitAsync();
        });

        Assert.True(await TableExistsAsync("retryable_ambient_test"));
    }

    private WalkamonContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WalkamonContext>()
            .UseSqlServer(
                ConnectionString,
                sqlServerOptions => sqlServerOptions.EnableRetryOnFailure())
            .Options;

        return new WalkamonContext(options, new HttpContextAccessor());
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(@tableName, 'U') IS NULL THEN 0 ELSE 1 END";
        command.Parameters.AddWithValue("@tableName", $"dbo.{tableName}");

        return (int)(await command.ExecuteScalarAsync())! == 1;
    }
}
