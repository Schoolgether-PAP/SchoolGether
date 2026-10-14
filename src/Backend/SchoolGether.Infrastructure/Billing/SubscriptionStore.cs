using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Billing;
using SchoolGether.Domain.Billing;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Billing;

internal sealed class SubscriptionStore(SchoolGetherDbContext db) : ISubscriptionStore
{
    public async Task<InstitutionSubscription?> FindForInstitutionAsync(int institutionId, CancellationToken cancellationToken)
    {
        var value = await db.Subscriptions.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == institutionId, cancellationToken);
        return value is null ? null : new InstitutionSubscription(value.Status,
            new SubscriptionPlan(value.PlanId, value.PlanNameKey, value.MonthlyPrice, value.Currency, value.AccountLimit), value.CatalogVersion, value.UsedAccounts);
    }

    public async Task CreateAsync(int institutionId, SubscriptionPlan plan, string catalogVersion, int ownerId, CancellationToken cancellationToken)
    {
        var scope = db.OnboardingScope ?? throw new InvalidOperationException("OnboardingTransactionRequired");
        var subscription = new DemonstrationSubscription(institutionId, plan, catalogVersion, ownerId);
        scope.BindSubscription(subscription);
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);
    }
}
