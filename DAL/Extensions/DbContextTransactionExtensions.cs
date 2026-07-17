using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DAL.Extensions;

public static class DbContextTransactionExtensions
{
    public static Task ExecuteInTransactionAsync(
        this DbContext context,
        IsolationLevel isolationLevel,
        Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);

        return context.ExecuteInTransactionAsync<object?>(
            isolationLevel,
            async () =>
            {
                await operation();
                return null;
            });
    }

    public static async Task<TResult> ExecuteInTransactionAsync<TResult>(
        this DbContext context,
        IsolationLevel isolationLevel,
        Func<Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);

        // An outer retryable unit already owns the transaction. Starting another
        // transaction here would both nest transactions and violate EF Core's
        // retrying execution-strategy requirements.
        if (context.Database.CurrentTransaction != null)
        {
            return await operation();
        }

        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database
                .BeginTransactionAsync(isolationLevel);

            try
            {
                var result = await operation();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}
