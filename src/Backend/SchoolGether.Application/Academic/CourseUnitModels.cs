using SchoolGether.Domain.Academic;
namespace SchoolGether.Application.Academic;

public sealed class AuthorizedCourseUnitScope
{
    internal AuthorizedCourseUnitScope(int actorId, string actorName, int institutionId, string operation, int courseId, int? unitId)
    { ActorId = actorId; ActorName = actorName; InstitutionId = institutionId; Operation = operation; CourseId = courseId; UnitId = unitId; }
    public int ActorId { get; }
    public string ActorName { get; }
    public int InstitutionId { get; }
    public string Operation { get; }
    public int CourseId { get; }
    public int? UnitId { get; }
}
public sealed record CourseUnitQuery(int Page = 1, int PageSize = 20, string? Search = null, string Status = "Active");
public sealed record CreateCourseUnit(Guid ClientId, string Code, string Name, string? Syllabus);
public sealed record UpdateCourseUnit(long RowVersion, string Code, string Name, string? Syllabus);
public sealed record CourseUnitResult(int Id, int InstitutionId, int CourseId, Guid ClientId, string Code, string Name, string? Syllabus,
    bool IsActive, long RowVersion, DateTime CreatedAt, DateTime? UpdatedAt, bool Replay = false)
{
    public static CourseUnitResult From(CourseUnit x, bool replay = false) => new(x.Id, x.InstitutionId, x.CourseId, x.ClientId, x.Code, x.Name, x.Syllabus, x.IsActive, x.RowVersion, x.CreatedAt, x.UpdatedAt, replay);
}
public sealed record CourseUnitPage(IReadOnlyList<CourseUnitResult> Items, int Total, int Page, int PageSize);
public interface IAcademicCourseUnitStore
{
    Task<Course?> FindCourseAsync(AuthorizedCourseUnitScope scope, CancellationToken ct);
    Task<CourseUnitPage> ListAsync(AuthorizedCourseUnitScope scope, CourseUnitQuery query, CancellationToken ct);
    Task<CourseUnit?> FindAsync(AuthorizedCourseUnitScope scope, int id, CancellationToken ct);
    Task<CourseUnit?> FindByClientIdAsync(AuthorizedCourseUnitScope scope, Guid clientId, CancellationToken ct);
    void Bind(AuthorizedCourseUnitScope scope, Course course, CourseUnit unit);
    void Add(AuthorizedCourseUnitScope scope, Course course, CourseUnit unit);
}
public interface ICourseUnitOfWork
{
    Task<T> ExecuteAsync<T>(AuthorizedCourseUnitScope scope, Func<CancellationToken, Task<T>> operation, CancellationToken ct);
}
