using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SchoolGether.Application.Academic;
using SchoolGether.Application.Audit;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Learning;
namespace SchoolGether.Application.Learning;
public sealed record LearningMaterialInput(Guid ClientId, int CourseId, int ClassId, int UnitId, string AcademicYear, string Title, string? Content, string? Url, bool IsPublished);
public sealed record LearningMaterialEdit(string Title, string? Content, string? Url, bool IsPublished);
public sealed class LearningMaterialScope
{
    internal LearningMaterialScope(int actor, string name, int tenant, string role, string operation, int? id) { ActorId = actor; ActorName = name; InstitutionId = tenant; Role = role; Operation = operation; MaterialId = id; }
    public int ActorId { get; } public string ActorName { get; } public int InstitutionId { get; } public string Role { get; } public string Operation { get; } public int? MaterialId { get; }
}
public interface ILearningMaterialStore
{
    Task<(IReadOnlyList<LearningMaterial> Items, int Total)> ListAsync(LearningMaterialScope s, string year, IReadOnlyList<LearningContext> contexts, int page, int size, CancellationToken ct);
    Task<LearningMaterial?> FindAsync(LearningMaterialScope s, int id, CancellationToken ct); Task<LearningMaterial?> FindByKeyAsync(LearningMaterialScope s, Guid key, CancellationToken ct);
    void Add(LearningMaterialScope s, LearningMaterial p); void Bind(LearningMaterialScope s, LearningMaterial p);
    Task<T> ExecuteAsync<T>(LearningMaterialScope s, Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
public sealed class LearningMaterialService(ICurrentAccount current, IIdentityStore identities, ILearningContextDirectory contexts, ILearningMaterialStore store, IAuditWriter audit, TimeProvider clock)
{
    async Task<LearningMaterialScope> Scope(string operation, int? id, CancellationToken ct)
    {
        var account = current.UserId is int uid ? await identities.FindForAuthenticationAsync(uid, ct) : null;
        if (account is null || account.InstitutionId != current.InstitutionId || account.Role.ToString() != current.Role) throw new LearningMaterialException("InvalidSession");
        if (current.Role is not ("Teacher" or "Student") || account.InstitutionId is not int tenant || (operation != "read" && current.Role != "Teacher")) throw new LearningMaterialException("Forbidden");
        return new(account.Id, account.Name, tenant, current.Role!, operation, id);
    }
    static bool HasContext(IEnumerable<LearningContext> rows, int course, int group, int unit) => rows.Any(x => x.CourseId == course && x.ClassId == group && x.UnitId == unit);
    public async Task<(IReadOnlyList<LearningMaterial> Items, int Total)> ListAsync(string year, int page, int pageSize, CancellationToken ct)
    { var s = await Scope("read", null, ct); if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue) throw new LearningMaterialException("InvalidInput"); var allowed = await contexts.ListAsync(year, ct); return await store.ListAsync(s, year, allowed, page, pageSize, ct); }
    public async Task<LearningMaterial> CreateAsync(LearningMaterialInput input, CancellationToken ct)
    {
        var s = await Scope("create", null, ct); var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        var candidate = LearningMaterial.Create(s.InstitutionId, s.ActorId, input.ClientId, input.CourseId, input.ClassId, input.UnitId, input.AcademicYear, input.Title, input.Content, input.Url, input.IsPublished, hash, clock.GetUtcNow().UtcDateTime);
        try { return await store.ExecuteAsync(s, async token => { if (!await contexts.LockForWriteAsync(input.CourseId,input.ClassId,input.UnitId,input.AcademicYear,token)) throw new LearningMaterialException("NotFound"); var allowed = await contexts.ListAsync(input.AcademicYear, token); if (!HasContext(allowed, input.CourseId, input.ClassId, input.UnitId)) throw new LearningMaterialException("NotFound"); var existing = await store.FindByKeyAsync(s, input.ClientId, token); if (existing is not null) { if (existing.CreateHash != hash) throw new LearningMaterialException("VersionConflict"); return existing; } store.Add(s, candidate); Append(s, candidate); return candidate; }, ct); }
        catch (LearningMaterialException e) when (e.Code == "VersionConflict") { var allowed = await contexts.ListAsync(input.AcademicYear, ct); if (!HasContext(allowed, input.CourseId, input.ClassId, input.UnitId)) throw new LearningMaterialException("NotFound"); var existing = await store.FindByKeyAsync(s, input.ClientId, ct); if (existing is null || existing.CreateHash != hash) throw; return existing; }
    }
    public async Task<LearningMaterial> ChangeAsync(int id, long version, LearningMaterialEdit? edit, CancellationToken ct)
    {
        var s = await Scope(edit is null ? "archive" : "update", id, ct);
        return await store.ExecuteAsync(s, async token => { var p = await store.FindAsync(s, id, token) ?? throw new LearningMaterialException("NotFound"); if (!await contexts.LockForWriteAsync(p.CourseId,p.ClassId,p.UnitId,p.AcademicYear,token)) throw new LearningMaterialException("NotFound"); var allowed = await contexts.ListAsync(p.AcademicYear, token); if (!HasContext(allowed, p.CourseId, p.ClassId, p.UnitId)) throw new LearningMaterialException("NotFound"); store.Bind(s, p); if (edit is null) p.Archive(version, clock.GetUtcNow().UtcDateTime); else p.Update(version, edit.Title, edit.Content, edit.Url, edit.IsPublished, clock.GetUtcNow().UtcDateTime); Append(s, p); return p; }, ct);
    }
    void Append(LearningMaterialScope s, LearningMaterial p) => audit.Append(new(new(s.ActorId, s.ActorName), "learning.material." + s.Operation, "learning.material:" + p.ClientId.ToString("D"), "success", "Confirmed", s.InstitutionId));
}
