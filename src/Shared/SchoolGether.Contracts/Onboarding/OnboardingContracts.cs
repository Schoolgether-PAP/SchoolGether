namespace SchoolGether.Contracts.Onboarding;

public sealed record RegisterInstitutionRequest(string PlanId, string CatalogVersion, string OwnerName, string Email,
    string Password, string InstitutionName, string InstitutionType, string? Locality, bool AcceptDemonstration, string? AcademicYear = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] SignupInstitutionDetails? Details = null);
public sealed record SignupInstitutionDetails(string ContactEmail, string ContactRole, string? Phone,
    string? Address, string? PostalCode, string? TaxNumber, int ExpectedTeachers);
public sealed record RegistrationResponse(int InstitutionId, int OwnerId, string Email);
