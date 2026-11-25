using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace SchoolGether.Contracts.Academic;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAcademicEnrolmentRequest(Guid ClientId,[Range(1,int.MaxValue)] int StudentId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcademicEnrolmentStateRequest([Range(1,long.MaxValue)] long RowVersion);
public sealed record AcademicStudentResponse(int StudentId,string Name,string Email,bool Available);
public sealed record AcademicStudentPageResponse(IReadOnlyList<AcademicStudentResponse> Items,int Total,int Page,int PageSize);
public sealed record AcademicEnrolmentResponse(int Id,int InstitutionId,int CourseId,int ClassId,int StudentId,Guid ClientId,int StudyYear,string AcademicYear,bool IsActive,long RowVersion,DateTime CreatedAt,DateTime? UpdatedAt,AcademicStudentResponse? Student,bool Replay=false);
public sealed record AcademicEnrolmentPageResponse(IReadOnlyList<AcademicEnrolmentResponse> Items,int Total,int Page,int PageSize);
