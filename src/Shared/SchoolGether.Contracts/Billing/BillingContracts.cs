namespace SchoolGether.Contracts.Billing;

public sealed record PlanResponse(string Id, string Name, decimal MonthlyPrice, string Currency, int AccountLimit);

public sealed record PlanCatalogResponse(string Version, bool RegistrationEnabled, IReadOnlyList<PlanResponse> Plans);
