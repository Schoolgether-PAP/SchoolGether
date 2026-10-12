using System.ComponentModel.DataAnnotations;

namespace SchoolGether.Contracts.Institutions;

public sealed record CreateInstitutionRequest([Required, StringLength(200)] string Name);
public sealed record InstitutionResponse(int Id, string Name);
