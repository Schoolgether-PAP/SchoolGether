using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SchoolGether.Application.Audit;
using SchoolGether.Application.Platform;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Audit;

internal sealed class AdministrativeUnitOfWork(SchoolGetherDbContext db) : IAdministrativeUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw new PlatformException("VersionConflict");
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 } ||
            (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" &&
             exception.InnerException?.Message.Contains("UNIQUE constraint failed:", StringComparison.Ordinal) == true))
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw new PlatformException(exception.InnerException!.Message.Contains("master_slot", StringComparison.OrdinalIgnoreCase)
                ? "MasterExists" : "AccountExists");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
