using SchoolGether.Domain.Billing;

namespace SchoolGether.Application.Billing;

public interface ISubscriptionStore
{
    Task CreateAsync(int institutionId, SubscriptionPlan plan, string catalogVersion, int ownerId, CancellationToken cancellationToken);
}
