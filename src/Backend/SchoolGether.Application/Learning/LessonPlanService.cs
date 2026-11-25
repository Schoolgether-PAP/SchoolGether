using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SchoolGether.Application.Academic;
using SchoolGether.Application.Audit;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Calendar;
using SchoolGether.Domain.Learning;
namespace SchoolGether.Application.Learning;
public sealed record LessonPlanInput(Guid ClientId, int CourseId, int ClassId, int UnitId, string AcademicYear, string Title, DateOnly Date, string? Objectives, string? Content, string? Resources);
public sealed record LessonPlanEdit(string Title, DateOnly Date, string? Objectives, string? Content, string? Resources);
public sealed class LessonPlanScope
{
    internal LessonPlanScope(int actor, string name, int tenant, string operation, int? id) { ActorId = actor; ActorName = name; InstitutionId = tenant; Operation = operation; PlanId = id; }
    public int ActorId { get; } public string ActorName { get; } public int InstitutionId { get; } public string Operation { get; } public int? PlanId { get; }
}
public interface ILessonPlanStore
{
    Task<IReadOnlyList<LessonPlan>> ListAsync(LessonPlanScope s, string year, CancellationToken ct);
    Task<LessonPlan?> FindAsync(LessonPlanScope s, int id, CancellationToken ct);
    Task<LessonPlan?> FindByKeyAsync(LessonPlanScope s, Guid key, CancellationToken ct);
    void Add(LessonPlanScope s, LessonPlan plan); void Bind(LessonPlanScope s, LessonPlan plan);
    Task<T> ExecuteAsync<T>(LessonPlanScope s, Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
public sealed class LessonPlanService(ICurrentAccount current, IIdentityStore identities, ILearningContextDirectory contexts, ILessonPlanStore store, IAuditWriter audit, TimeProvider clock)
{
    async Task<LessonPlanScope> Scope(string operation, int? id, CancellationToken ct)
    {
        var account = current.UserId is int uid ? await identities.FindForAuthenticationAsync(uid, ct) : null;
        if (account is null || account.InstitutionId != current.InstitutionId || account.Role.ToString() != current.Role) throw new CalendarException("InvalidSession");
        if (current.Role != "Teacher" || account.InstitutionId is not int tenant) throw new CalendarException("Forbidden");
        return new(account.Id, account.Name, tenant, operation, id);
    }
    static bool HasContext(IEnumerable<LearningContext> contexts, int course, int group, int unit) => contexts.Any(x => x.CourseId == course && x.ClassId == group && x.UnitId == unit);
    public async Task<(IReadOnlyList<LessonPlan> Items, int Total)> ListAsync(string year, int page, int pageSize, CancellationToken ct)
    { var s = await Scope("read", null, ct); if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue) throw new CalendarException("InvalidInput"); var allowed = await contexts.ListAsync(year, ct); var rows = (await store.ListAsync(s, year, ct)).Where(p => HasContext(allowed, p.CourseId, p.ClassId, p.UnitId)).ToArray(); return (rows.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), rows.Length); }
    public async Task<LessonPlan> CreateAsync(LessonPlanInput input, CancellationToken ct)
    {
        var s = await Scope("create", null, ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        var candidate = LessonPlan.Create(s.InstitutionId, s.ActorId, input.ClientId, input.CourseId, input.ClassId, input.UnitId, input.AcademicYear, input.Title, input.Date, input.Objectives, input.Content, input.Resources, hash, clock.GetUtcNow().UtcDateTime);
        try
        {
            return await store.ExecuteAsync(s, async token => { if (!await contexts.LockForWriteAsync(input.CourseId,input.ClassId,input.UnitId,input.AcademicYear,token)) throw new CalendarException("NotFound"); var allowed = await contexts.ListAsync(input.AcademicYear, token); if (!HasContext(allowed, input.CourseId, input.ClassId, input.UnitId)) throw new CalendarException("NotFound"); var existing = await store.FindByKeyAsync(s, input.ClientId, token); if (existing is not null) { if (existing.CreateHash != hash) throw new CalendarException("VersionConflict"); return existing; } store.Add(s, candidate); Append(s, candidate); return candidate; }, ct);
        }
        catch(CalendarException e) when(e.Code=="VersionConflict")
        {
            var allowed=await contexts.ListAsync(input.AcademicYear,ct);
            if(!HasContext(allowed,input.CourseId,input.ClassId,input.UnitId))throw new CalendarException("NotFound");
            var existing=await store.FindByKeyAsync(s,input.ClientId,ct);
            if(existing is null||existing.CreateHash!=hash)throw;
            return existing;
        }
    }
    public async Task<LessonPlan> ChangeAsync(int id, long version, LessonPlanEdit? edit, CancellationToken ct)
    {
        var s = await Scope(edit is null ? "archive" : "update", id, ct);
        return await store.ExecuteAsync(s, async token => { var p = await store.FindAsync(s, id, token) ?? throw new CalendarException("NotFound"); if (!await contexts.LockForWriteAsync(p.CourseId,p.ClassId,p.UnitId,p.AcademicYear,token)) throw new CalendarException("NotFound"); var allowed = await contexts.ListAsync(p.AcademicYear, token); if (!HasContext(allowed, p.CourseId, p.ClassId, p.UnitId)) throw new CalendarException("NotFound"); store.Bind(s, p); if (edit is null) p.Archive(version, clock.GetUtcNow().UtcDateTime); else p.Update(version, edit.Title, edit.Date, edit.Objectives, edit.Content, edit.Resources, clock.GetUtcNow().UtcDateTime); Append(s, p); return p; }, ct);
    }
    void Append(LessonPlanScope s, LessonPlan p) => audit.Append(new(new(s.ActorId, s.ActorName), "learning.plan." + s.Operation, "learning.plan:" + p.ClientId.ToString("D"), "success", "Confirmed", s.InstitutionId));
}
