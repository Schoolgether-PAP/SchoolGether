using SchoolGether.Domain.Billing;

namespace SchoolGether.Application.Billing;

public interface ISubscriptionStore
{
    Task<InstitutionSubscription?> FindForInstitutionAsync(int institutionId, CancellationToken cancellationToken);
    Task CreateAsync(int institutionId, SubscriptionPlan plan, string catalogVersion, int ownerId, CancellationToken cancellationToken);
}

public sealed record InstitutionSubscription(string Status, SubscriptionPlan? Plan, string? CatalogVersion, int UsedAccounts);
