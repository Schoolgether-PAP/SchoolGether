namespace SchoolGether.Domain.Billing;

public sealed class DemonstrationSubscription
{
    private DemonstrationSubscription() { }
    public DemonstrationSubscription(int institutionId, SubscriptionPlan plan, string catalogVersion, int createdBy)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(institutionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(createdBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogVersion);
        InstitutionId = institutionId; CreatedBy = createdBy;
        PlanId = plan.Id; PlanNameKey = plan.NameKey; Price = plan.Price;
        BillingPeriod = plan.BillingPeriod; CapacityUnit = plan.CapacityUnit;
        UsedAccounts = plan.CapacityUnit == "Teachers" ? 0 : 1;
        Currency = plan.Currency; AccountLimit = plan.AccountLimit; CatalogVersion = catalogVersion;
    }
    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public string Status { get; private set; } = "Demonstration";
    public string PlanId { get; private set; } = string.Empty;
    public string PlanNameKey { get; private set; } = string.Empty;
    public string CatalogVersion { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string BillingPeriod { get; private set; } = "Monthly";
    public string CapacityUnit { get; private set; } = "Accounts";
    public string Currency { get; private set; } = string.Empty;
    public int AccountLimit { get; private set; }
    public int UsedAccounts { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public long RowVersion { get; private set; } = 1;
}
