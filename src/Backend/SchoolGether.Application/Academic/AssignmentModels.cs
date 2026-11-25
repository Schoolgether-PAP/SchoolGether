using SchoolGether.Domain.Academic;
using SchoolGether.Application.Identity;
namespace SchoolGether.Application.Academic;
public sealed class AuthorizedAssignmentScope
{
    internal AuthorizedAssignmentScope(int actorId,string actorName,int institutionId,string operation,int courseId,int classId,int? assignmentId)
    {ActorId=actorId;ActorName=actorName;InstitutionId=institutionId;Operation=operation;CourseId=courseId;ClassId=classId;AssignmentId=assignmentId;}
    public int ActorId{get;} public string ActorName{get;} public int InstitutionId{get;} public string Operation{get;} public int CourseId{get;} public int ClassId{get;} public int? AssignmentId{get;}
}
public sealed record AssignmentQuery(int Page=1,int PageSize=20,string? Search=null,string Status="Active");
public sealed record TeacherQuery(int Page=1,int PageSize=20,string? Search=null);
public sealed record CreateAssignment(Guid ClientId,int TeacherId,int UnitId);
public sealed record AssignmentUnitDescription(int Id,string Code,string Name);
public sealed record AcademicAssignmentResult(int Id,int InstitutionId,int CourseId,int ClassId,int TeacherId,int UnitId,Guid ClientId,int StudyYear,string AcademicYear,bool IsActive,long RowVersion,DateTime CreatedAt,DateTime? UpdatedAt,TeacherDirectoryItem? Teacher,bool Replay=false,AssignmentUnitDescription? Unit=null)
{
    public static AcademicAssignmentResult From(AcademicAssignment x,TeacherDirectoryItem? teacher,bool replay=false)=>new(x.Id,x.InstitutionId,x.CourseId,x.ClassId,x.TeacherId,x.UnitId,x.ClientId,x.StudyYear,x.AcademicYear,x.IsActive,x.RowVersion,x.CreatedAt,x.UpdatedAt,teacher,replay);
}
public sealed record AcademicAssignmentPage(IReadOnlyList<AcademicAssignmentResult> Items,int Total,int Page,int PageSize);
public sealed record AssignmentRows(IReadOnlyList<AcademicAssignment> Items,int Total);
public interface IAcademicAssignmentStore
{
    Task<bool> IsSelectedUnitAsync(AuthorizedAssignmentScope scope,int unitId,CancellationToken ct);
    Task<IReadOnlyList<AssignmentUnitDescription>> DescribeUnitsAsync(AuthorizedAssignmentScope scope,IReadOnlyList<int> unitIds,CancellationToken ct);
    Task<Course?> FindCourseAsync(AuthorizedAssignmentScope scope,CancellationToken ct);
    Task<AcademicClass?> FindClassAsync(AuthorizedAssignmentScope scope,CancellationToken ct);
    Task<AcademicAssignment?> FindAsync(AuthorizedAssignmentScope scope,int id,CancellationToken ct);
    Task<AcademicAssignment?> FindByClientIdAsync(AuthorizedAssignmentScope scope,Guid clientId,CancellationToken ct);
    Task<AssignmentRows> ListAsync(AuthorizedAssignmentScope scope,AssignmentQuery query,IReadOnlyList<int>? teacherIds,CancellationToken ct);
    Task<IReadOnlyList<int>> ExcludedTeachersAsync(AuthorizedAssignmentScope scope,string academicYear,CancellationToken ct);
    void Bind(AuthorizedAssignmentScope scope,Course course,AcademicClass value,AcademicAssignment assignment,TeacherDirectoryItem? teacher);
    void Add(AuthorizedAssignmentScope scope,AcademicAssignment assignment);
}
public interface IAssignmentUnitOfWork
{Task<T> ExecuteAsync<T>(AuthorizedAssignmentScope scope,Func<CancellationToken,Task<T>> operation,CancellationToken ct);}
