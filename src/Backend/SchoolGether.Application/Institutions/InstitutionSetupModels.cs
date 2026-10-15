namespace SchoolGether.Application.Institutions;

public sealed record InstitutionSetup(int InstitutionId, string Name, string InstitutionType, string? Locality,
    string? AcademicYear, bool IsComplete, long RowVersion, string SuggestedAcademicYear = "");
public sealed record SaveInstitutionSetup(string Name, string InstitutionType, string? Locality, string AcademicYear, long RowVersion);
