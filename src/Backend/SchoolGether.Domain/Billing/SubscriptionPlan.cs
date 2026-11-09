namespace SchoolGether.Domain.Billing;

public sealed class SubscriptionPlan
{
    public SubscriptionPlan(string id, string nameKey, decimal price, string currency, int accountLimit, string billingPeriod = "Monthly", string capacityUnit = "Accounts")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountLimit);

        if (billingPeriod is not ("Monthly" or "Annual")) throw new ArgumentException("InvalidBillingPeriod", nameof(billingPeriod));
        if (capacityUnit is not ("Accounts" or "Teachers")) throw new ArgumentException("InvalidCapacityUnit", nameof(capacityUnit));
        BillingPeriod = billingPeriod;
        CapacityUnit = capacityUnit;
        Id = id;
        NameKey = nameKey;
        Price = price;
        Currency = currency;
        AccountLimit = accountLimit;
    }

    public string Id { get; }
    public string NameKey { get; }
    public decimal Price { get; }
    public string BillingPeriod { get; }
    public string CapacityUnit { get; }
    public string Currency { get; }
    public int AccountLimit { get; }
}
