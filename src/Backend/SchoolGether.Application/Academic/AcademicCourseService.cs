using SchoolGether.Application.Audit;
using SchoolGether.Domain.Academic;

namespace SchoolGether.Application.Academic;

public sealed class AcademicCourseService(AcademicAuthorization authorization, IAcademicCourseStore store, IAcademicUnitOfWork work, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CoursePage> ListAsync(CourseQuery query, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync("read", ct: ct);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue ||
            query.Search?.Length > 150 || query.Status is not ("Active" or "Archived")) throw new AcademicException("InvalidCourseInput");
        return await store.ListAsync(scope, query, ct);
    }
    public async Task<CourseResult> GetAsync(int id, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync("read", id, ct);
        return CourseResult.From(await Find(scope, id, ct));
    }
    public async Task<CourseResult> CreateAsync(CreateCourse request, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync("create", ct: ct);
        var initial = Course.Create(scope.InstitutionId, scope.ActorId, request.ClientId, request.Code, request.Name, request.Description, clock.GetUtcNow().UtcDateTime);
        try
        {
            var saved = await work.ExecuteAsync(scope, async token =>
            {
                var existing = await store.FindByClientIdAsync(scope, request.ClientId, token);
                if (existing is not null) return Replay(existing, initial);
                store.Add(scope, initial); Append(scope, initial);
                return (Course: initial, Replay: false);
            }, ct);
            return CourseResult.From(saved.Course, saved.Replay);
        }
        catch (AcademicException ex) when (ex.Code == "CourseConflict")
        {
            // Uma corrida de criação pode já ter confirmado a mesma chave inicial.
            var existing = await store.FindByClientIdAsync(scope, request.ClientId, ct);
            if (existing is null) throw;
            var replay = Replay(existing, initial); return CourseResult.From(replay.Course, replay.Replay);
        }
    }
    public Task<CourseResult> UpdateAsync(int id, UpdateCourse request, CancellationToken ct) => Change(id, "update", c => c.Update(request.RowVersion, request.Code, request.Name, request.Description, clock.GetUtcNow().UtcDateTime), ct);
    public Task<CourseResult> ArchiveAsync(int id, long rowVersion, CancellationToken ct) => Change(id, "archive", c => c.Archive(rowVersion, clock.GetUtcNow().UtcDateTime), ct);
    public Task<CourseResult> RestoreAsync(int id, long rowVersion, CancellationToken ct) => Change(id, "restore", c => c.Restore(rowVersion, clock.GetUtcNow().UtcDateTime), ct);
    private async Task<CourseResult> Change(int id, string operation, Action<Course> change, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync(operation, id, ct);
        var course = await work.ExecuteAsync(scope, async token =>
        {
            var value = await Find(scope, id, token); store.Bind(scope, value); change(value); Append(scope, value); return value;
        }, ct);
        return CourseResult.From(course);
    }
    private async Task<Course> Find(AuthorizedAcademicScope scope, int id, CancellationToken ct) => await store.FindAsync(scope, id, ct) ?? throw new AcademicException("CourseNotFound");
    private static (Course Course, bool Replay) Replay(Course existing, Course initial)
    {
        if (!existing.IsReplayOf(initial)) throw new AcademicException("CourseReplayConflict");
        return (existing, true);
    }
    private void Append(AuthorizedAcademicScope scope, Course value) => audit.Append(new(new(scope.ActorId, scope.ActorName),
        "academic." + scope.Operation, "academic.course:" + value.ClientId.ToString("D"), "success", "Confirmed", scope.InstitutionId));
}
