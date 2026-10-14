using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Billing;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Billing;

internal sealed class InstitutionAccountCapacity(SchoolGetherDbContext db, ICurrentAccount current, TimeProvider clock)
    : IInstitutionAccountCapacity
{
    public async Task ReserveAsync(int institutionId, CancellationToken cancellationToken)
    {
        // ExecuteUpdate bypasses SaveChanges and query filters cannot select a global
        // administrator's target. Authorize this narrow write before bypassing them.
        if (institutionId <= 0 || current.UserId is not > 0 ||
            (current.Role != nameof(AccountRole.PlatformAdmin) &&
             (current.Role != nameof(AccountRole.InstitutionAdmin) || current.InstitutionId != institutionId)))
            throw new InvalidOperationException("TenantWriteDenied");
        if (db.Database.CurrentTransaction is null || db.OnboardingScope is not null)
            throw new InvalidOperationException("AccountTransactionRequired");

        var subscription = db.Subscriptions.IgnoreQueryFilters()
            .Where(x => x.InstitutionId == institutionId && x.DeletedAt == null);
        var now = clock.GetUtcNow().UtcDateTime;
        var reserved = await subscription.Where(x => x.UsedAccounts < x.AccountLimit)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.UsedAccounts, x => x.UsedAccounts + 1)
                .SetProperty(x => x.RowVersion, x => x.RowVersion + 1)
                .SetProperty(x => x.UpdatedAt, now), cancellationToken);
        if (reserved == 0 && await subscription.AnyAsync(cancellationToken))
            throw new BillingException("InstitutionAccountLimitReached");
        // No subscription: keep the institution's existing legacy behavior.
    }
}
