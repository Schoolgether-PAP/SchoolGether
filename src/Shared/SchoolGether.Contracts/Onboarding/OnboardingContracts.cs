namespace SchoolGether.Contracts.Onboarding;

public sealed record RegisterInstitutionRequest(string PlanId, string CatalogVersion, string OwnerName, string Email,
    string Password, string InstitutionName, string InstitutionType, string? Locality, bool AcceptDemonstration);
public sealed record RegistrationResponse(int InstitutionId, int OwnerId, string Email);
