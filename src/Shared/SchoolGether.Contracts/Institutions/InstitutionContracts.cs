using System.ComponentModel.DataAnnotations;

namespace SchoolGether.Contracts.Institutions;

public sealed record CreateInstitutionRequest([Required, StringLength(200)] string Name);
public sealed record InstitutionResponse(int Id, string Name);
public sealed record InstitutionSetupResponse(int InstitutionId, string Name, string InstitutionType, string? Locality,
    string? AcademicYear, bool IsComplete, long RowVersion, string SuggestedAcademicYear);
public sealed record SaveInstitutionSetupRequest([Required, StringLength(200)] string Name, [Required, StringLength(32)] string InstitutionType,
    [StringLength(100)] string? Locality, [Required, StringLength(9)] string AcademicYear, [Range(1, long.MaxValue)] long RowVersion);
