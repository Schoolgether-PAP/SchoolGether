using SchoolGether.Application.Audit;
using SchoolGether.Domain.Academic;
using SchoolGether.Domain.Institutions;
namespace SchoolGether.Application.Academic;

public sealed class AcademicClassService(AcademicAuthorization authorization, IAcademicClassStore store, IClassUnitOfWork work, IAuditWriter audit, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public async Task<AcademicClassPage> ListAsync(int courseId, AcademicClassQuery query, CancellationToken ct)
    {
        var scope = await authorization.RequireClassAsync("read", courseId, ct: ct);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue || query.Search?.Length > 150 || query.Status is not ("Active" or "Archived") || query.StudyYear is < 1 or > 20 || query.AcademicYear is not null && !InstitutionProfile.IsValidAcademicYear(query.AcademicYear)) throw new AcademicException("InvalidClassInput");
        await Parent(scope, ct); return await store.ListAsync(scope, query, ct);
    }
    public async Task<AcademicClassResult> GetAsync(int courseId, int classId, CancellationToken ct)
    {
        var scope = await authorization.RequireClassAsync("read", courseId, classId, ct); await Parent(scope, ct);
        var value = await Class(scope, classId, ct); return AcademicClassResult.From(value, await store.LinksAsync(scope, value, ct));
    }
    public async Task<AcademicClassResult> CreateAsync(int courseId, CreateAcademicClass request, CancellationToken ct)
    {
        var scope = await authorization.RequireClassAsync("create", courseId, ct: ct);
        var initial = AcademicClass.Create(scope.InstitutionId, courseId, scope.ActorId, request.ClientId, request.Code, request.Name, request.StudyYear, request.AcademicYear, request.UnitIds, Now);
        try
        {
            var saved = await work.ExecuteAsync(scope, async token =>
            {
                var parent = await Parent(scope, token);
                var existing = await store.FindByClientIdAsync(scope, request.ClientId, token);
                if (existing is not null) return await Replay(scope, existing, initial, token);
                var units = await Selection(scope, initial, request.UnitIds, [], token);
                store.Bind(scope, parent, initial, [], units); store.Add(scope, initial);
                var links = units.Select(u => AcademicClassUnit.Create(initial, u, scope.ActorId, Now)).ToList();
                foreach (var link in links) store.AddLink(scope, link);
                parent.TouchClasses(parent.RowVersion, Now); Append(scope, initial);
                return (Class: initial, Links: links, Replay: false);
            }, ct);
            return AcademicClassResult.From(saved.Class, saved.Links, saved.Replay);
        }
        catch (AcademicException ex) when (ex.Code is "ClassConflict" or "VersionConflict")
        {
            var existing = await store.FindByClientIdAsync(scope, request.ClientId, ct); if (existing is null) throw;
            var saved = await Replay(scope, existing, initial, ct); return AcademicClassResult.From(saved.Class, saved.Links, true);
        }
    }
    public Task<AcademicClassResult> UpdateAsync(int courseId, int classId, UpdateAcademicClass request, CancellationToken ct) => Change(courseId, classId, "update", request.RowVersion, request, ct);
    public Task<AcademicClassResult> ArchiveAsync(int courseId, int classId, long rowVersion, CancellationToken ct) => Change(courseId, classId, "archive", rowVersion, null, ct);
    public Task<AcademicClassResult> RestoreAsync(int courseId, int classId, long rowVersion, CancellationToken ct) => Change(courseId, classId, "restore", rowVersion, null, ct);
    private async Task<AcademicClassResult> Change(int courseId, int classId, string operation, long version, UpdateAcademicClass? request, CancellationToken ct)
    {
        var scope = await authorization.RequireClassAsync(operation, courseId, classId, ct);
        var saved = await work.ExecuteAsync(scope, async token =>
        {
            var parent = await Parent(scope, token); var value = await Class(scope, classId, token); var links = await store.LinksAsync(scope, value, token);
            var units = request is null ? links.Where(l => l.IsActive).Select(l => l.Unit).ToList() : await Selection(scope, value, request.UnitIds, links, token);
            store.Bind(scope, parent, value, links, units); parent.TouchClasses(parent.RowVersion, Now);
            if (request is not null)
            {
                value.Update(version, request.Code, request.Name, Now);
                foreach (var link in links) link.SetSelected(request.UnitIds.Contains(link.UnitId), Now);
                foreach (var unit in units.Where(u => links.All(l => l.UnitId != u.Id))) { var link = AcademicClassUnit.Create(value, unit, scope.ActorId, Now); store.AddLink(scope, link); links.Add(link); }
            }
            else if (operation == "archive") value.Archive(version, Now); else value.Restore(version, Now);
            Append(scope, value); return (Class: value, Links: links);
        }, ct);
        return AcademicClassResult.From(saved.Class, saved.Links);
    }
    private async Task<List<CourseUnit>> Selection(AuthorizedClassScope scope, AcademicClass value, IReadOnlyList<int> ids, IReadOnlyList<AcademicClassUnit> links, CancellationToken ct)
    {
        var sorted = AcademicClass.ValidateUnitIds(ids); var units = await store.UnitsAsync(scope, sorted, ct);
        if (units.Count != sorted.Length) throw new AcademicException("CourseUnitNotFound");
        if (units.Any(u => u.StudyYear != value.StudyYear)) throw new AcademicException("InvalidClassSelection");
        if (units.Any(u => !u.IsActive && !links.Any(l => l.UnitId == u.Id && l.IsActive))) throw new AcademicException("ClassUnitArchived");
        return units;
    }
    private async Task<Course> Parent(AuthorizedClassScope scope, CancellationToken ct) => await store.FindCourseAsync(scope, ct) ?? throw new AcademicException("CourseNotFound");
    private async Task<AcademicClass> Class(AuthorizedClassScope scope, int id, CancellationToken ct) => await store.FindAsync(scope, id, ct) ?? throw new AcademicException("ClassNotFound");
    private async Task<(AcademicClass Class, List<AcademicClassUnit> Links, bool Replay)> Replay(AuthorizedClassScope scope, AcademicClass existing, AcademicClass initial, CancellationToken ct)
    { if (!existing.IsReplayOf(initial)) throw new AcademicException("ClassReplayConflict"); return (existing, await store.LinksAsync(scope, existing, ct), true); }
    private void Append(AuthorizedClassScope scope, AcademicClass value) => audit.Append(new(new(scope.ActorId, scope.ActorName), "academic.class." + scope.Operation, "academic.class:" + value.ClientId.ToString("D"), "success", "Confirmed", scope.InstitutionId));
}
