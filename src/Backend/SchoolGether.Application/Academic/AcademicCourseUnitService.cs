using SchoolGether.Application.Audit;
using SchoolGether.Domain.Academic;
namespace SchoolGether.Application.Academic;

public sealed class AcademicCourseUnitService(AcademicAuthorization authorization, IAcademicCourseUnitStore store, ICourseUnitOfWork work, IAuditWriter audit, TimeProvider clock)
{
    public async Task<CourseUnitPage> ListAsync(int courseId, CourseUnitQuery query, CancellationToken ct)
    {
        var scope = await authorization.RequireUnitAsync("read", courseId, ct: ct);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue || query.Search?.Length > 150 || query.Status is not ("Active" or "Archived")) throw new AcademicException("InvalidCourseUnitInput");
        await Parent(scope, ct); return await store.ListAsync(scope, query, ct);
    }
    public async Task<CourseUnitResult> GetAsync(int courseId, int unitId, CancellationToken ct)
    {
        var scope = await authorization.RequireUnitAsync("read", courseId, unitId, ct); await Parent(scope, ct);
        return CourseUnitResult.From(await Unit(scope, unitId, ct));
    }
    public async Task<CourseUnitResult> CreateAsync(int courseId, CreateCourseUnit request, CancellationToken ct)
    {
        var scope = await authorization.RequireUnitAsync("create", courseId, ct: ct);
        var initial = CourseUnit.Create(scope.InstitutionId, courseId, scope.ActorId, request.ClientId, request.Code, request.Name, request.Syllabus, clock.GetUtcNow().UtcDateTime);
        try
        {
            var saved = await work.ExecuteAsync(scope, async token =>
            {
                var parent = await Parent(scope, token);
                var existing = await store.FindByClientIdAsync(scope, request.ClientId, token);
                if (existing is not null) return Replay(existing, initial);
                store.Add(scope, parent, initial); parent.TouchUnits(parent.RowVersion, clock.GetUtcNow().UtcDateTime); Append(scope, initial);
                return (Unit: initial, Replay: false);
            }, ct);
            return CourseUnitResult.From(saved.Unit, saved.Replay);
        }
        catch (AcademicException ex) when (ex.Code is "CourseUnitConflict" or "VersionConflict")
        {
            // A concorrência do pai também pode esconder uma criação já confirmada.
            var existing = await store.FindByClientIdAsync(scope, request.ClientId, ct);
            if (existing is null) throw;
            var replay = Replay(existing, initial); return CourseUnitResult.From(replay.Unit, replay.Replay);
        }
    }
    public Task<CourseUnitResult> UpdateAsync(int courseId, int unitId, UpdateCourseUnit request, CancellationToken ct) => Change(courseId, unitId, "update", u => u.Update(request.RowVersion, request.Code, request.Name, request.Syllabus, clock.GetUtcNow().UtcDateTime), ct);
    public Task<CourseUnitResult> ArchiveAsync(int courseId, int unitId, long rowVersion, CancellationToken ct) => Change(courseId, unitId, "archive", u => u.Archive(rowVersion, clock.GetUtcNow().UtcDateTime), ct);
    public Task<CourseUnitResult> RestoreAsync(int courseId, int unitId, long rowVersion, CancellationToken ct) => Change(courseId, unitId, "restore", u => u.Restore(rowVersion, clock.GetUtcNow().UtcDateTime), ct);
    private async Task<CourseUnitResult> Change(int courseId, int unitId, string operation, Action<CourseUnit> change, CancellationToken ct)
    {
        var scope = await authorization.RequireUnitAsync(operation, courseId, unitId, ct);
        var unit = await work.ExecuteAsync(scope, async token =>
        {
            var parent = await Parent(scope, token); var value = await Unit(scope, unitId, token);
            store.Bind(scope, parent, value); parent.TouchUnits(parent.RowVersion, clock.GetUtcNow().UtcDateTime); change(value); Append(scope, value); return value;
        }, ct);
        return CourseUnitResult.From(unit);
    }
    private async Task<Course> Parent(AuthorizedCourseUnitScope scope, CancellationToken ct) => await store.FindCourseAsync(scope, ct) ?? throw new AcademicException("CourseNotFound");
    private async Task<CourseUnit> Unit(AuthorizedCourseUnitScope scope, int unitId, CancellationToken ct) => await store.FindAsync(scope, unitId, ct) ?? throw new AcademicException("CourseUnitNotFound");
    private static (CourseUnit Unit, bool Replay) Replay(CourseUnit existing, CourseUnit initial)
    { if (!existing.IsReplayOf(initial)) throw new AcademicException("CourseUnitReplayConflict"); return (existing, true); }
    private void Append(AuthorizedCourseUnitScope scope, CourseUnit unit) => audit.Append(new(new(scope.ActorId, scope.ActorName), "academic.unit." + scope.Operation, "academic.unit:" + unit.ClientId.ToString("D"), "success", "Confirmed", scope.InstitutionId));
}
