namespace SchoolGether.Contracts.Billing;

public sealed record PlanResponse(string Id, string Name, decimal Price, string Currency, int AccountLimit, string BillingPeriod = "Monthly", string CapacityUnit = "Accounts");

public sealed record PlanCatalogResponse(string Version, bool RegistrationEnabled, IReadOnlyList<PlanResponse> Plans);

public sealed record InstitutionSubscriptionResponse(string Status, PlanResponse? Plan, string? CatalogVersion, int UsedAccounts);
