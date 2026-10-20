using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SchoolGether.Application.Support;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Support;

internal sealed class SupportUnitOfWork(SchoolGetherDbContext db) : ISupportUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(AuthorizedSupportScope scope, Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        if (db.ClassScope is not null) throw new InvalidOperationException("ClassWriteDenied");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.SupportScope = new SupportWriteScope(db, scope);
        try
        {
            var result = await operation(ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            throw new SupportException("VersionConflict");
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 or 1213 or 1205 } ||
            (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" && ex.InnerException?.Message.Contains("UNIQUE constraint failed:", StringComparison.Ordinal) == true))
        {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            throw new SupportException("VersionConflict");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw;
        }
        finally { db.SupportScope = null; }
    }
}
