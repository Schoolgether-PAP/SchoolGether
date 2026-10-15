using SchoolGether.Domain.Billing;

namespace SchoolGether.Application.Billing;

public sealed class PlanCatalog
{
    private static readonly IReadOnlyList<SubscriptionPlan> Plans = Array.AsReadOnly(new[] {
        new SubscriptionPlan("essential", "PlanEssential", 59m, "EUR", 100),
        new SubscriptionPlan("evolution", "PlanEvolution", 149m, "EUR", 350),
        new SubscriptionPlan("campus", "PlanCampus", 299m, "EUR", 1150)
    });

    public string Version => "2026-10-14-v1";

    public IReadOnlyList<SubscriptionPlan> GetPlans() => Plans;

    public SubscriptionPlan RequirePlan(string planId, string version)
    {
        if (!string.Equals(version, Version, StringComparison.Ordinal))
            throw new BillingException("OutdatedCatalog");

        return Plans.FirstOrDefault(plan => string.Equals(plan.Id, planId, StringComparison.Ordinal))
            ?? throw new BillingException("UnknownPlan");
    }
}

public sealed class BillingException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
