using SchoolGether.Domain.Academic;

namespace SchoolGether.Application.Academic;

public sealed class AcademicException(string code) : Exception(code) { public string Code { get; } = code; }
public sealed record CourseQuery(int Page = 1, int PageSize = 20, string? Search = null, string Status = "Active");
public sealed record CreateCourse(Guid ClientId, string Code, string Name, string? Description);
public sealed record UpdateCourse(long RowVersion, string Code, string Name, string? Description);
public sealed record CourseResult(int Id, int InstitutionId, Guid ClientId, string Code, string Name, string? Description,
    bool IsActive, long RowVersion, DateTime CreatedAt, DateTime? UpdatedAt, bool Replay = false)
{
    public static CourseResult From(Course x, bool replay = false) => new(x.Id, x.InstitutionId, x.ClientId, x.Code, x.Name,
        x.Description, x.IsActive, x.RowVersion, x.CreatedAt, x.UpdatedAt, replay);
}
public sealed record CoursePage(IReadOnlyList<CourseResult> Items, int Total, int Page, int PageSize);

public interface IAcademicCourseStore
{
    Task<CoursePage> ListAsync(AuthorizedAcademicScope scope, CourseQuery query, CancellationToken ct);
    Task<Course?> FindAsync(AuthorizedAcademicScope scope, int id, CancellationToken ct);
    Task<Course?> FindByClientIdAsync(AuthorizedAcademicScope scope, Guid clientId, CancellationToken ct);
    void Add(AuthorizedAcademicScope scope, Course course);
    void Bind(AuthorizedAcademicScope scope, Course course);
}
public interface IAcademicUnitOfWork
{
    Task<T> ExecuteAsync<T>(AuthorizedAcademicScope scope, Func<CancellationToken, Task<T>> operation, CancellationToken ct);
}
