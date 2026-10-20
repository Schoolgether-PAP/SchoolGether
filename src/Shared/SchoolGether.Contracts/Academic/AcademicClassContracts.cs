using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace SchoolGether.Contracts.Academic;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAcademicClassRequest(Guid ClientId, [Required, TrimmedLength(30)] string Code, [Required, TrimmedLength(200)] string Name,
    [Range(1,20)] int StudyYear, [Required, StringLength(9)] string AcademicYear, [Required, MinLength(1), MaxLength(100)] IReadOnlyList<int> UnitIds);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateAcademicClassRequest([Range(1,long.MaxValue)] long RowVersion, [Required, TrimmedLength(30)] string Code,
    [Required, TrimmedLength(200)] string Name, [Required, MinLength(1), MaxLength(100)] IReadOnlyList<int> UnitIds);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcademicClassStateRequest([Range(1,long.MaxValue)] long RowVersion);
public sealed record AcademicClassUnitResponse(int UnitId, string Code, string Name, int StudyYear, bool IsActive);
public sealed record AcademicClassResponse(int Id, int InstitutionId, int CourseId, Guid ClientId, string Code, string Name, int StudyYear, string AcademicYear,
    bool IsActive, long RowVersion, DateTime CreatedAt, DateTime? UpdatedAt, IReadOnlyList<AcademicClassUnitResponse> Units, bool Replay = false);
public sealed record AcademicClassListItemResponse(int Id, int InstitutionId, int CourseId, Guid ClientId, string Code, string Name, int StudyYear, string AcademicYear,
    bool IsActive, long RowVersion, DateTime CreatedAt, DateTime? UpdatedAt, int UnitCount);
public sealed record AcademicClassPageResponse(IReadOnlyList<AcademicClassListItemResponse> Items, int Total, int Page, int PageSize);
