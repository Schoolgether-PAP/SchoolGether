using SchoolGether.Domain.Academic;
namespace SchoolGether.Application.Academic;

public sealed class AuthorizedClassScope
{
    internal AuthorizedClassScope(int actorId, string actorName, int institutionId, string operation, int courseId, int? classId)
    { ActorId = actorId; ActorName = actorName; InstitutionId = institutionId; Operation = operation; CourseId = courseId; ClassId = classId; }
    public int ActorId { get; }
    public string ActorName { get; }
    public int InstitutionId { get; }
    public string Operation { get; }
    public int CourseId { get; }
    public int? ClassId { get; }
}
public sealed record AcademicClassQuery(int Page = 1, int PageSize = 20, string? Search = null, string Status = "Active", int? StudyYear = null, string? AcademicYear = null);
public sealed record CreateAcademicClass(Guid ClientId, string Code, string Name, int StudyYear, string AcademicYear, IReadOnlyList<int> UnitIds);
public sealed record UpdateAcademicClass(long RowVersion, string Code, string Name, IReadOnlyList<int> UnitIds);
public sealed record AcademicClassUnitResult(int UnitId, string Code, string Name, int StudyYear, bool IsActive);
public sealed record AcademicClassResult(int Id, int InstitutionId, int CourseId, Guid ClientId, string Code, string Name, int StudyYear, string AcademicYear,
    bool IsActive, long RowVersion, DateTime CreatedAt, DateTime? UpdatedAt, IReadOnlyList<AcademicClassUnitResult> Units, bool Replay = false)
{
    public static AcademicClassResult From(AcademicClass x, IEnumerable<AcademicClassUnit> links, bool replay = false) => new(x.Id, x.InstitutionId, x.CourseId, x.ClientId, x.Code, x.Name, x.StudyYear, x.AcademicYear, x.IsActive, x.RowVersion, x.CreatedAt, x.UpdatedAt,
        links.Where(l => l.IsActive).OrderBy(l => l.UnitId).Select(l => new AcademicClassUnitResult(l.UnitId, l.Unit.Code, l.Unit.Name, l.Unit.StudyYear!.Value, l.Unit.IsActive)).ToArray(), replay);
}
public sealed record AcademicClassListItem(int Id, int InstitutionId, int CourseId, Guid ClientId, string Code, string Name, int StudyYear, string AcademicYear,
    bool IsActive, long RowVersion, DateTime CreatedAt, DateTime? UpdatedAt, int UnitCount);
public sealed record AcademicClassPage(IReadOnlyList<AcademicClassListItem> Items, int Total, int Page, int PageSize);
public interface IAcademicClassStore
{
    Task<Course?> FindCourseAsync(AuthorizedClassScope scope, CancellationToken ct);
    Task<AcademicClass?> FindAsync(AuthorizedClassScope scope, int id, CancellationToken ct);
    Task<AcademicClass?> FindByClientIdAsync(AuthorizedClassScope scope, Guid clientId, CancellationToken ct);
    Task<AcademicClassPage> ListAsync(AuthorizedClassScope scope, AcademicClassQuery query, CancellationToken ct);
    Task<List<AcademicClassUnit>> LinksAsync(AuthorizedClassScope scope, AcademicClass value, CancellationToken ct);
    Task<List<CourseUnit>> UnitsAsync(AuthorizedClassScope scope, IReadOnlyList<int> ids, CancellationToken ct);
    void Bind(AuthorizedClassScope scope, Course parent, AcademicClass value, IReadOnlyList<AcademicClassUnit> links, IReadOnlyList<CourseUnit> eligible);
    void Add(AuthorizedClassScope scope, AcademicClass value);
    void AddLink(AuthorizedClassScope scope, AcademicClassUnit link);
}
public interface IClassUnitOfWork
{
    Task<T> ExecuteAsync<T>(AuthorizedClassScope scope, Func<CancellationToken, Task<T>> operation, CancellationToken ct);
}
