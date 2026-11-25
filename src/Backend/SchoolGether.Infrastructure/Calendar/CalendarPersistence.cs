using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Calendar;
using SchoolGether.Domain.Calendar;
using SchoolGether.Domain.Audit;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Infrastructure.Calendar;

public static class DependencyInjection
{ public static IServiceCollection AddCalendarModule(this IServiceCollection services) => services.AddScoped<CalendarService>().AddScoped<ICalendarStore, CalendarStore>(); }
internal sealed class CalendarConfiguration : IEntityTypeConfiguration<SchoolEvent>
{
    public void Configure(EntityTypeBuilder<SchoolEvent> b)
    {
        b.ToTable("calendar_event"); b.HasKey(x => x.Id); b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.Details).HasMaxLength(4000); b.Property(x => x.Location).HasMaxLength(200); b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.RowVersion).IsConcurrencyToken(); b.HasIndex(x => new { x.InstitutionId, x.StartUtc }); b.HasIndex(x => new { x.InstitutionId, x.ClientId }).IsUnique(); b.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        foreach (var p in b.Metadata.GetProperties()) p.SetColumnName(string.Concat(p.Name.Select((ch, i) => char.IsUpper(ch) && i > 0 ? "_" + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString())));
    }
}
internal sealed class CalendarWriteScope(SchoolGetherDbContext db, CalendarScope scope)
{
    readonly Guid transaction = db.Database.CurrentTransaction?.TransactionId ?? throw new InvalidOperationException("CalendarTransactionRequired"); SchoolEvent? bound;
    internal void Bind(CalendarScope s, SchoolEvent e)
    { if (!ReferenceEquals(s, scope) || db.Database.CurrentTransaction?.TransactionId != transaction || e.InstitutionId != scope.InstitutionId || scope.EventId.HasValue && e.Id != scope.EventId || bound is not null && !ReferenceEquals(bound, e)) throw new InvalidOperationException("CalendarWriteDenied"); bound = e; }
    internal bool Allows(EntityEntry entry)
    {
        if (bound is null || db.Database.CurrentTransaction?.TransactionId != transaction) return false;
        if (entry.Entity is AuditEntry a) return entry.State == EntityState.Added && a.ActorId == scope.ActorId && a.InstitutionId == scope.InstitutionId && a.Action == "calendar." + scope.Operation && a.Target == "calendar.event:" + bound.ClientId.ToString("D") && a.Result == "success";
        if (!ReferenceEquals(entry.Entity, bound) || bound.InstitutionId != scope.InstitutionId || scope.Role is not ("Teacher" or "InstitutionAdmin")) return false;
        if (entry.State == EntityState.Added) return scope.Operation == "create" && bound.Id == 0 && bound.RowVersion == 1 && bound.CreatedBy == scope.ActorId && bound.Status == (scope.Role == "Teacher" ? "Pending" : "Published");
        if (entry.State != EntityState.Modified || entry.Property("InstitutionId").OriginalValue is not int tenant || tenant != scope.InstitutionId || (long)entry.Property("RowVersion").OriginalValue! + 1 != bound.RowVersion) return false;
        var old = (string)entry.Property("Status").OriginalValue!;
        if (scope.Role == "Teacher" && (bound.CreatedBy != scope.ActorId || old != "Pending" || scope.Operation is not ("update" or "cancel"))) return false;
        if (scope.Operation == "update" && (old is not ("Pending" or "Published") || bound.Status != old)) return false;
        if (scope.Operation == "approve" && (old != "Pending" || bound.Status != "Published")) return false;
        if (scope.Operation == "reject" && (old != "Pending" || bound.Status != "Rejected")) return false;
        if (scope.Operation == "cancel" && (old is not ("Pending" or "Published") || bound.Status != "Cancelled")) return false;
        if (scope.Operation is not ("update" or "approve" or "reject" or "cancel")) return false;
        var allowed = scope.Operation == "update" ? new[] { "Title", "Details", "Location", "StartUtc", "EndUtc", "RowVersion", "UpdatedAt" } : new[] { "Status", "RowVersion", "UpdatedAt" }; return entry.Properties.Where(x => x.IsModified).All(x => allowed.Contains(x.Metadata.Name));
    }
}
internal sealed class CalendarStore(SchoolGetherDbContext db) : ICalendarStore
{
    IQueryable<SchoolEvent> Query(CalendarScope s) => db.CalendarEvents.Where(x => x.InstitutionId == s.InstitutionId);
    public async Task<CalendarPage> ListAsync(CalendarScope s, CalendarQuery q, CancellationToken ct)
    { var from = q.From.UtcDateTime; var to = q.To.UtcDateTime; var rows = Query(s).AsNoTracking().Where(x => x.StartUtc < to && x.EndUtc > from && x.Status == q.Status); var total = await rows.CountAsync(ct); var items = await rows.OrderBy(x => x.StartUtc).ThenBy(x => x.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct); return new(items.Select(EventResult.From).ToArray(), total, q.Page, q.PageSize); }
    public Task<SchoolEvent?> FindAsync(CalendarScope s, int id, CancellationToken ct) => Query(s).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<SchoolEvent?> FindByClientIdAsync(CalendarScope s, Guid clientId, CancellationToken ct) => Query(s).SingleOrDefaultAsync(x => x.ClientId == clientId, ct);
    public void Bind(CalendarScope s, SchoolEvent e) => (db.CalendarScope ?? throw new InvalidOperationException("CalendarWriteDenied")).Bind(s, e);
    public void Add(CalendarScope s, SchoolEvent e) { Bind(s, e); db.CalendarEvents.Add(e); }
    public async Task<T> ExecuteAsync<T>(CalendarScope s, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (db.AssignmentScope is not null || db.EnrolmentScope is not null || db.CalendarScope is not null || db.AcademicScope is not null || db.ClassScope is not null || db.CourseUnitScope is not null || db.SupportScope is not null || db.OnboardingScope is not null) throw new InvalidOperationException("CalendarWriteDenied"); await using var tx = await db.Database.BeginTransactionAsync(ct); db.CalendarScope = new(db, s);
        try { var result = await action(ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result; }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new CalendarException("VersionConflict"); }
        catch (DbUpdateException e) when (e.InnerException is MySql.Data.MySqlClient.MySqlException { Number: 1062 }) { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new CalendarException("VersionConflict"); }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        finally { db.CalendarScope = null; }
    }
}
