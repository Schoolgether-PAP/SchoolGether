using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SchoolGether.Application.Onboarding;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Onboarding;

internal sealed class OnboardingUnitOfWork(SchoolGetherDbContext db) : IOnboardingUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        if (db.ClassScope is not null) throw new InvalidOperationException("ClassWriteDenied");
        if (db.OnboardingScope is not null) throw new InvalidOperationException("OnboardingAlreadyStarted");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.OnboardingScope = new OnboardingWriteScope(db);
        try
        {
            var result = await operation(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException exception) when (IsDuplicateAccount(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw new OnboardingException("SignupEmailUnavailable");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
        finally { db.OnboardingScope = null; }
    }
    private static bool IsDuplicateAccount(DbUpdateException exception) =>
        exception.Entries.Any(x => x.Entity is Identity.ApplicationUser) &&
        (exception.InnerException is MySqlException { Number: 1062 } ||
         exception.InnerException?.Message.Contains("UNIQUE constraint failed: user_account.", StringComparison.Ordinal) == true);
}
