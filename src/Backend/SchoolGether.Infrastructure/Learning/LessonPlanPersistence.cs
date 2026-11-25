using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Learning;
using SchoolGether.Domain.Audit;
using SchoolGether.Domain.Calendar;
using SchoolGether.Domain.Institutions;
using SchoolGether.Domain.Learning;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Infrastructure.Learning;
public static class DependencyInjection
{ public static IServiceCollection AddLearningPlans(this IServiceCollection services) => services.AddScoped<LessonPlanService>().AddScoped<ILessonPlanStore, LessonPlanStore>(); }
internal sealed class LessonPlanConfiguration : IEntityTypeConfiguration<LessonPlan>
{
    public void Configure(EntityTypeBuilder<LessonPlan> b)
    {
        b.Property(x=>x.Date).HasConversion(x=>x.ToDateTime(TimeOnly.MinValue),x=>DateOnly.FromDateTime(x)).HasColumnType("date");
        b.ToTable("learning_lesson_plan"); b.HasKey(x => x.Id); b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.AcademicYear).HasMaxLength(9); b.Property(x => x.Objectives).HasMaxLength(4000).HasColumnType("text"); b.Property(x => x.Content).HasMaxLength(8000).HasColumnType("text"); b.Property(x => x.Resources).HasMaxLength(4000).HasColumnType("text"); b.Property(x => x.CreateHash).HasMaxLength(64); b.Property(x => x.RowVersion).IsConcurrencyToken(); b.HasIndex(x => new { x.InstitutionId, x.TeacherId, x.ClientId }).IsUnique(); b.HasIndex(x => new { x.InstitutionId, x.TeacherId, x.AcademicYear }); b.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        foreach (var p in b.Metadata.GetProperties()) p.SetColumnName(string.Concat(p.Name.Select((ch, i) => char.IsUpper(ch) && i > 0 ? "_" + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString())));
    }
}
internal sealed class LessonPlanWriteScope(SchoolGetherDbContext db, LessonPlanScope scope)
{
    readonly Guid transaction = db.Database.CurrentTransaction?.TransactionId ?? throw new InvalidOperationException("LessonPlanTransactionRequired"); LessonPlan? bound;
    internal void Bind(LessonPlanScope s, LessonPlan p)
    { if (!ReferenceEquals(s, scope) || db.Database.CurrentTransaction?.TransactionId != transaction || p.InstitutionId != scope.InstitutionId || p.TeacherId != scope.ActorId || scope.PlanId.HasValue && p.Id != scope.PlanId || bound is not null && !ReferenceEquals(bound, p)) throw new InvalidOperationException("LessonPlanWriteDenied"); bound = p; }
    internal bool Allows(EntityEntry entry)
    {
        if (bound is null || db.Database.CurrentTransaction?.TransactionId != transaction) return false;
        if (entry.Entity is AuditEntry a) return entry.State == EntityState.Added && a.ActorId == scope.ActorId && a.InstitutionId == scope.InstitutionId && a.Action == "learning.plan." + scope.Operation && a.Target == "learning.plan:" + bound.ClientId.ToString("D") && a.Result == "success";
        if (!ReferenceEquals(entry.Entity, bound) || bound.InstitutionId != scope.InstitutionId || bound.TeacherId != scope.ActorId) return false;
        if (entry.State == EntityState.Added) return scope.Operation == "create" && bound.Id == 0 && bound.RowVersion == 1 && !bound.IsArchived;
        if (entry.State != EntityState.Modified || (int)entry.Property("InstitutionId").OriginalValue! != scope.InstitutionId || (int)entry.Property("TeacherId").OriginalValue! != scope.ActorId || (long)entry.Property("RowVersion").OriginalValue! + 1 != bound.RowVersion || (bool)entry.Property("IsArchived").OriginalValue!) return false;
        if (scope.Operation == "update" && bound.IsArchived || scope.Operation == "archive" && !bound.IsArchived || scope.Operation is not ("update" or "archive")) return false;
        var allowed = scope.Operation == "update" ? new[] { "Title", "Date", "Objectives", "Content", "Resources", "RowVersion", "UpdatedAt" } : new[] { "IsArchived", "RowVersion", "UpdatedAt" };
        return entry.Properties.Where(x => x.IsModified).All(x => allowed.Contains(x.Metadata.Name));
    }
}
internal sealed class LessonPlanStore(SchoolGetherDbContext db) : ILessonPlanStore
{
    IQueryable<LessonPlan> Query(LessonPlanScope s) => db.LessonPlans.Where(x => x.InstitutionId == s.InstitutionId && x.TeacherId == s.ActorId);
    public async Task<IReadOnlyList<LessonPlan>> ListAsync(LessonPlanScope s, string year, CancellationToken ct) => await Query(s).AsNoTracking().Where(x => x.AcademicYear == year && !x.IsArchived).OrderBy(x => x.Date).ThenBy(x => x.Id).ToArrayAsync(ct);
    public Task<LessonPlan?> FindAsync(LessonPlanScope s, int id, CancellationToken ct) => Query(s).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<LessonPlan?> FindByKeyAsync(LessonPlanScope s, Guid key, CancellationToken ct) => Query(s).SingleOrDefaultAsync(x => x.ClientId == key, ct);
    public void Bind(LessonPlanScope s, LessonPlan p) => (db.LessonPlanScope ?? throw new InvalidOperationException("LessonPlanWriteDenied")).Bind(s, p);
    public void Add(LessonPlanScope s, LessonPlan p) { Bind(s, p); db.LessonPlans.Add(p); }
    public async Task<T> ExecuteAsync<T>(LessonPlanScope s, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (db.LessonPlanScope is not null || db.AssignmentScope is not null || db.EnrolmentScope is not null || db.CalendarScope is not null || db.AcademicScope is not null || db.ClassScope is not null || db.CourseUnitScope is not null || db.SupportScope is not null || db.OnboardingScope is not null) throw new InvalidOperationException("LessonPlanWriteDenied");
        await using var tx = await db.Database.BeginTransactionAsync(ct); db.LessonPlanScope = new(db, s);
        try { var result = await action(ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result; }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new CalendarException("VersionConflict"); }
        catch (DbUpdateException e) when (e.InnerException is MySql.Data.MySqlClient.MySqlException { Number: 1062 or 1213 or 1205 } || e.InnerException?.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException") { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new CalendarException("VersionConflict"); }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        finally { db.LessonPlanScope = null; }
    }
}
