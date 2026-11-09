using SchoolGether.Domain.Billing;

namespace SchoolGether.Application.Billing;

public sealed class PlanCatalog
{
    private static readonly IReadOnlyList<SubscriptionPlan> Plans = Array.AsReadOnly(new[] {
        new SubscriptionPlan("essential", "PlanEssential", 1000m, "EUR", 20, "Annual", "Teachers"),
        new SubscriptionPlan("evolution", "PlanProfessional", 2500m, "EUR", 50, "Annual", "Teachers"),
        new SubscriptionPlan("campus", "PlanInstitutional", 4000m, "EUR", 100, "Annual", "Teachers")
    });

    public string Version => "2026-11-09-v1";

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
