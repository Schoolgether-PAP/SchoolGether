using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace SchoolGether.Contracts.Academic;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAcademicAssignmentRequest(Guid ClientId,[Range(1,int.MaxValue)] int TeacherId,[Range(1,int.MaxValue)] int UnitId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcademicAssignmentStateRequest([Range(1,long.MaxValue)] long RowVersion);
public sealed record AcademicTeacherResponse(int TeacherId,string Name,string Email,bool Available);
public sealed record AcademicTeacherPageResponse(IReadOnlyList<AcademicTeacherResponse> Items,int Total,int Page,int PageSize);
public sealed record AcademicAssignmentUnitResponse(int Id,string Code,string Name);
public sealed record AcademicAssignmentResponse(int Id,int InstitutionId,int CourseId,int ClassId,int TeacherId,int UnitId,Guid ClientId,int StudyYear,string AcademicYear,bool IsActive,long RowVersion,DateTime CreatedAt,DateTime? UpdatedAt,AcademicTeacherResponse? Teacher,bool Replay=false,AcademicAssignmentUnitResponse? Unit=null);
public sealed record AcademicAssignmentPageResponse(IReadOnlyList<AcademicAssignmentResponse> Items,int Total,int Page,int PageSize);
