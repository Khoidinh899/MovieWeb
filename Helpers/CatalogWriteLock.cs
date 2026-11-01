using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MovieWeb.Data;

namespace MovieWeb.Helpers;

public static class CatalogWriteLock
{
    // Shared with the local importer. A global key also serializes shared taxonomy writes.
    public const int Namespace = 741927;
    public const int Key = 1;

    public static async Task<IDbContextTransaction> AcquireAsync(MovieWebDbContext context)
    {
        if (context.Database.CurrentTransaction is not null || context.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Catalog sync requires a clean, standalone unit of work.");

        var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '1s'; SET LOCAL statement_timeout = '5s';");
            await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(741927, 1);");
            // A reused scope may hold an older graph from before another writer committed.
            context.ChangeTracker.Clear();
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
