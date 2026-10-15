namespace SchoolGether.Domain.Billing;

public sealed class SubscriptionPlan
{
    public SubscriptionPlan(string id, string nameKey, decimal monthlyPrice, string currency, int accountLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountLimit);

        Id = id;
        NameKey = nameKey;
        MonthlyPrice = monthlyPrice;
        Currency = currency;
        AccountLimit = accountLimit;
    }

    public string Id { get; }
    public string NameKey { get; }
    public decimal MonthlyPrice { get; }
    public string Currency { get; }
    public int AccountLimit { get; }
}
