using SchoolGether.Domain.Academic;
using SchoolGether.Application.Identity;
namespace SchoolGether.Application.Academic;
public sealed class AuthorizedEnrolmentScope
{
    internal AuthorizedEnrolmentScope(int actorId,string actorName,int institutionId,string operation,int courseId,int classId,int? enrolmentId)
    {ActorId=actorId;ActorName=actorName;InstitutionId=institutionId;Operation=operation;CourseId=courseId;ClassId=classId;EnrolmentId=enrolmentId;}
    public int ActorId{get;} public string ActorName{get;} public int InstitutionId{get;} public string Operation{get;} public int CourseId{get;} public int ClassId{get;} public int? EnrolmentId{get;}
}
public sealed record EnrolmentQuery(int Page=1,int PageSize=20,string? Search=null,string Status="Active");
public sealed record StudentQuery(int Page=1,int PageSize=20,string? Search=null);
public sealed record CreateEnrolment(Guid ClientId,int StudentId);
public sealed record AcademicEnrolmentResult(int Id,int InstitutionId,int CourseId,int ClassId,int StudentId,Guid ClientId,int StudyYear,string AcademicYear,bool IsActive,long RowVersion,DateTime CreatedAt,DateTime? UpdatedAt,StudentDirectoryItem? Student,bool Replay=false)
{
    public static AcademicEnrolmentResult From(AcademicEnrolment x,StudentDirectoryItem? student,bool replay=false)=>new(x.Id,x.InstitutionId,x.CourseId,x.ClassId,x.StudentId,x.ClientId,x.StudyYear,x.AcademicYear,x.IsActive,x.RowVersion,x.CreatedAt,x.UpdatedAt,student,replay);
}
public sealed record AcademicEnrolmentPage(IReadOnlyList<AcademicEnrolmentResult> Items,int Total,int Page,int PageSize);
public sealed record EnrolmentRows(IReadOnlyList<AcademicEnrolment> Items,int Total);
public interface IAcademicEnrolmentStore
{
    Task<Course?> FindCourseAsync(AuthorizedEnrolmentScope scope,CancellationToken ct);
    Task<AcademicClass?> FindClassAsync(AuthorizedEnrolmentScope scope,CancellationToken ct);
    Task<AcademicEnrolment?> FindAsync(AuthorizedEnrolmentScope scope,int id,CancellationToken ct);
    Task<AcademicEnrolment?> FindByClientIdAsync(AuthorizedEnrolmentScope scope,Guid clientId,CancellationToken ct);
    Task<EnrolmentRows> ListAsync(AuthorizedEnrolmentScope scope,EnrolmentQuery query,IReadOnlyList<int>? studentIds,CancellationToken ct);
    Task<IReadOnlyList<int>> ExcludedStudentsAsync(AuthorizedEnrolmentScope scope,string academicYear,CancellationToken ct);
    void Bind(AuthorizedEnrolmentScope scope,Course course,AcademicClass value,AcademicEnrolment enrolment,StudentDirectoryItem? student);
    void Add(AuthorizedEnrolmentScope scope,AcademicEnrolment enrolment);
}
public interface IEnrolmentUnitOfWork
{Task<T> ExecuteAsync<T>(AuthorizedEnrolmentScope scope,Func<CancellationToken,Task<T>> operation,CancellationToken ct);}
