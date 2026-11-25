using SchoolGether.Application.Identity;
using SchoolGether.Application.Audit;
using SchoolGether.Domain.Calendar;
namespace SchoolGether.Application.Calendar;

public sealed record EventInput(string Title, string? Details, string? Location, DateTimeOffset Start, DateTimeOffset End, Guid? ClientId = null);
public sealed record CalendarQuery(DateTimeOffset From, DateTimeOffset To, int Page = 1, int PageSize = 30, string Status = "Published");
public sealed record EventResult(int Id, string Title, string? Details, string? Location, DateTimeOffset Start, DateTimeOffset End, string Status, long RowVersion, int CreatedBy)
{ public static EventResult From(SchoolEvent e) => new(e.Id, e.Title, e.Details, e.Location, new(DateTime.SpecifyKind(e.StartUtc, DateTimeKind.Utc)), new(DateTime.SpecifyKind(e.EndUtc, DateTimeKind.Utc)), e.Status, e.RowVersion, e.CreatedBy); }
public sealed record CalendarPage(IReadOnlyList<EventResult> Items, int Total, int Page, int PageSize);
public sealed class CalendarScope
{
    internal CalendarScope(int actor, string name, int tenant, string role, string operation, int? id) { ActorId = actor; ActorName = name; InstitutionId = tenant; Role = role; Operation = operation; EventId = id; }
    public int ActorId { get; }
    public string ActorName { get; }
    public int InstitutionId { get; }
    public string Role { get; }
    public string Operation { get; }
    public int? EventId { get; }
}
public interface ICalendarStore
{ Task<CalendarPage> ListAsync(CalendarScope s, CalendarQuery q, CancellationToken ct); Task<SchoolEvent?> FindAsync(CalendarScope s, int id, CancellationToken ct); Task<SchoolEvent?> FindByClientIdAsync(CalendarScope s, Guid clientId, CancellationToken ct); void Add(CalendarScope s, SchoolEvent e); void Bind(CalendarScope s, SchoolEvent e); Task<T> ExecuteAsync<T>(CalendarScope s, Func<CancellationToken, Task<T>> action, CancellationToken ct); }
public sealed class CalendarService(ICurrentAccount current, IIdentityStore identities, ICalendarStore store, IAuditWriter audit, TimeProvider clock)
{
    private async Task<CalendarScope> Scope(string op, int? id, CancellationToken ct)
    {
        var a = current.UserId is int uid ? await identities.FindForAuthenticationAsync(uid, ct) : null;
        if (a is null || a.InstitutionId != current.InstitutionId || a.Role.ToString() != current.Role) throw new CalendarException("InvalidSession");
        if (a.InstitutionId is not > 0 || current.Role is not ("Teacher" or "Student" or "InstitutionAdmin")) throw new CalendarException("Forbidden");
        if (op != "read" && (current.Role == "Student" || op is "approve" or "reject" && current.Role != "InstitutionAdmin")) throw new CalendarException("Forbidden");
        return new(a.Id, a.Name, a.InstitutionId.Value, current.Role!, op, id);
    }
    public async Task<CalendarPage> ListAsync(CalendarQuery q, CancellationToken ct)
    { var s = await Scope("read", null, ct); if (q.From == default || q.To <= q.From || q.To - q.From > TimeSpan.FromDays(366) || q.Page < 1 || q.PageSize is < 1 or > 100 || (long)(q.Page - 1) * q.PageSize > int.MaxValue || q.Status is not ("Published" or "Pending" or "Rejected" or "Cancelled")) throw new CalendarException("InvalidInput"); if (s.Role == "Student" && q.Status != "Published") throw new CalendarException("Forbidden"); return await store.ListAsync(s, q, ct); }
    public async Task<EventResult> CreateAsync(EventInput input, CancellationToken ct)
    {
        var scope = await Scope("create", null, ct);
        var candidate = SchoolEvent.Create(scope.InstitutionId, scope.ActorId, scope.Role == "InstitutionAdmin", input.Title, input.Details, input.Location, input.Start, input.End, clock.GetUtcNow().UtcDateTime, input.ClientId);
        var value = await store.ExecuteAsync(scope, async token =>
        {
            if (input.ClientId is { } clientId && await store.FindByClientIdAsync(scope, clientId, token) is { } existing)
            {
                // A replay never changes an event, including one already approved or subsequently edited.
                if (existing.CreatedBy != scope.ActorId || existing.Title != candidate.Title || existing.Details != candidate.Details || existing.Location != candidate.Location || existing.StartUtc != candidate.StartUtc || existing.EndUtc != candidate.EndUtc)
                    throw new CalendarException("VersionConflict");
                return existing;
            }
            store.Add(scope, candidate);
            Append(scope, candidate);
            return candidate;
        }, ct);
        return EventResult.From(value);
    }
    public async Task<EventResult> ChangeAsync(int id, long version, string operation, EventInput? input, CancellationToken ct)
    {
        var s = await Scope(operation, id, ct); var value = await store.ExecuteAsync(s, async token =>
        {
            var e = await store.FindAsync(s, id, token) ?? throw new CalendarException("NotFound");
            if (s.Role == "Teacher" && (e.CreatedBy != s.ActorId || e.Status != "Pending")) throw new CalendarException("NotFound");
            store.Bind(s, e); if (operation == "update" && input is not null) e.Update(version, input.Title, input.Details, input.Location, input.Start, input.End, clock.GetUtcNow().UtcDateTime); else e.Change(version, operation, clock.GetUtcNow().UtcDateTime); Append(s, e); return e;
        }, ct); return EventResult.From(value);
    }
    private void Append(CalendarScope s, SchoolEvent e) => audit.Append(new(new(s.ActorId, s.ActorName), "calendar." + s.Operation, "calendar.event:" + e.ClientId.ToString("D"), "success", "Confirmed", s.InstitutionId));
}
