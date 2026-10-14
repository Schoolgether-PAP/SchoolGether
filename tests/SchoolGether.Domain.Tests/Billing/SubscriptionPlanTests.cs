using SchoolGether.Domain.Billing;

namespace SchoolGether.Domain.Tests.Billing;

public sealed class SubscriptionPlanTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_invalid_account_limit(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SubscriptionPlan("essential", "PlanEssential", 59m, "EUR", limit));
    }

    [Fact]
    public void Constructor_rejects_negative_price()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SubscriptionPlan("essential", "PlanEssential", -0.01m, "EUR", 100));
    }

    [Theory]
    [InlineData(null, "PlanEssential", "EUR")]
    [InlineData("", "PlanEssential", "EUR")]
    [InlineData(" ", "PlanEssential", "EUR")]
    [InlineData("essential", null, "EUR")]
    [InlineData("essential", "", "EUR")]
    [InlineData("essential", " ", "EUR")]
    [InlineData("essential", "PlanEssential", null)]
    [InlineData("essential", "PlanEssential", "")]
    [InlineData("essential", "PlanEssential", " ")]
    public void Constructor_rejects_missing_plan_identity(string? id, string? nameKey, string? currency)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SubscriptionPlan(id!, nameKey!, 59m, currency!, 100));
    }

    [Fact]
    public void Constructor_allows_zero_price_with_positive_capacity()
    {
        var plan = new SubscriptionPlan("free", "PlanFree", 0m, "EUR", 1);

        Assert.Equal(0m, plan.MonthlyPrice);
        Assert.Equal(1, plan.AccountLimit);
    }
}
