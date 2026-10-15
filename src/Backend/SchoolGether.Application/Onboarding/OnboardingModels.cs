namespace SchoolGether.Application.Onboarding;

public sealed record RegisterInstitution(string PlanId, string CatalogVersion, string OwnerName, string Email,
    string Password, string InstitutionName, string InstitutionType, string? Locality, bool AcceptDemonstration);
public sealed record Registration(int InstitutionId, int OwnerId, string Email);
public sealed record RegisteredOwner(int Id, string Name, string Email);
public sealed class OnboardingException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
public sealed class OnboardingOptions
{
    public bool Enabled { get; set; }
}
