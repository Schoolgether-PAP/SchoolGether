using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Academic;
using SchoolGether.Application.Learning;
using SchoolGether.Domain.Audit;
using SchoolGether.Domain.Institutions;
using SchoolGether.Domain.Learning;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Infrastructure.Learning;
public static class LearningMaterialDependencyInjection
{ public static IServiceCollection AddLearningMaterials(this IServiceCollection services) => services.AddScoped<LearningMaterialService>().AddScoped<ILearningMaterialStore, LearningMaterialStore>(); }
internal sealed class LearningMaterialConfiguration : IEntityTypeConfiguration<LearningMaterial>
{
    public void Configure(EntityTypeBuilder<LearningMaterial> b)
    {
        b.ToTable("learning_material"); b.HasKey(x => x.Id); b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.AcademicYear).HasMaxLength(9); b.Property(x => x.Content).HasMaxLength(8000).HasColumnType("text"); b.Property(x => x.Url).HasMaxLength(2048); b.Property(x => x.CreateHash).HasMaxLength(64); b.Property(x => x.RowVersion).IsConcurrencyToken(); b.HasIndex(x => new { x.InstitutionId, x.TeacherId, x.ClientId }).IsUnique(); b.HasIndex(x => new { x.InstitutionId, x.AcademicYear, x.CourseId, x.ClassId, x.UnitId }); b.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        foreach (var p in b.Metadata.GetProperties()) p.SetColumnName(string.Concat(p.Name.Select((ch, i) => char.IsUpper(ch) && i > 0 ? "_" + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString())));
    }
}
internal sealed class LearningMaterialWriteScope(SchoolGetherDbContext db, LearningMaterialScope scope)
{
    readonly Guid transaction = db.Database.CurrentTransaction?.TransactionId ?? throw new InvalidOperationException("LearningMaterialTransactionRequired"); LearningMaterial? bound;
    internal void Bind(LearningMaterialScope s, LearningMaterial p)
    { if (!ReferenceEquals(s, scope) || db.Database.CurrentTransaction?.TransactionId != transaction || p.InstitutionId != scope.InstitutionId || p.TeacherId != scope.ActorId || scope.MaterialId.HasValue && p.Id != scope.MaterialId || bound is not null && !ReferenceEquals(bound, p)) throw new InvalidOperationException("LearningMaterialWriteDenied"); bound = p; }
    internal bool Allows(EntityEntry entry)
    {
        if (bound is null || db.Database.CurrentTransaction?.TransactionId != transaction) return false;
        if (entry.Entity is AuditEntry a) return entry.State == EntityState.Added && a.ActorId == scope.ActorId && a.InstitutionId == scope.InstitutionId && a.Action == "learning.material." + scope.Operation && a.Target == "learning.material:" + bound.ClientId.ToString("D") && a.Result == "success";
        if (!ReferenceEquals(entry.Entity, bound) || bound.InstitutionId != scope.InstitutionId || bound.TeacherId != scope.ActorId) return false;
        if (entry.State == EntityState.Added) return scope.Operation == "create" && bound.Id == 0 && bound.RowVersion == 1 && !bound.IsArchived;
        if (entry.State != EntityState.Modified || (int)entry.Property("InstitutionId").OriginalValue! != scope.InstitutionId || (int)entry.Property("TeacherId").OriginalValue! != scope.ActorId || (long)entry.Property("RowVersion").OriginalValue! + 1 != bound.RowVersion || (bool)entry.Property("IsArchived").OriginalValue!) return false;
        if (scope.Operation == "update" && bound.IsArchived || scope.Operation == "archive" && !bound.IsArchived || scope.Operation is not ("update" or "archive")) return false;
        var allowed = scope.Operation == "update" ? new[] { "Title", "Content", "Url", "IsPublished", "RowVersion", "UpdatedAt" } : new[] { "IsArchived", "RowVersion", "UpdatedAt" };
        return entry.Properties.Where(x => x.IsModified).All(x => allowed.Contains(x.Metadata.Name));
    }
}
internal sealed class LearningMaterialStore(SchoolGetherDbContext db) : ILearningMaterialStore
{
    IQueryable<LearningMaterial> Own(LearningMaterialScope s) => db.LearningMaterials.Where(x => x.InstitutionId == s.InstitutionId && x.TeacherId == s.ActorId);
    public async Task<(IReadOnlyList<LearningMaterial> Items, int Total)> ListAsync(LearningMaterialScope s, string year, IReadOnlyList<LearningContext> contexts, int page, int size, CancellationToken ct)
    {
        var parameter = Expression.Parameter(typeof(LearningMaterial), "p"); Expression filter = Expression.Constant(false);
        foreach (var c in contexts) { Expression term = Expression.AndAlso(Expression.AndAlso(Expression.Equal(Expression.Property(parameter, nameof(LearningMaterial.CourseId)), Expression.Constant(c.CourseId)), Expression.Equal(Expression.Property(parameter, nameof(LearningMaterial.ClassId)), Expression.Constant(c.ClassId))), Expression.Equal(Expression.Property(parameter, nameof(LearningMaterial.UnitId)), Expression.Constant(c.UnitId))); filter = Expression.OrElse(filter, term); }
        var query = db.LearningMaterials.AsNoTracking().Where(x => x.InstitutionId == s.InstitutionId && x.AcademicYear == year && !x.IsArchived).Where(Expression.Lambda<Func<LearningMaterial, bool>>(filter, parameter));
        query = s.Role == "Teacher" ? query.Where(x => x.TeacherId == s.ActorId) : query.Where(x => x.IsPublished);
        var total = await query.CountAsync(ct); var items = await query.OrderBy(x => x.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct); return (items, total);
    }
    public Task<LearningMaterial?> FindAsync(LearningMaterialScope s, int id, CancellationToken ct) => Own(s).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<LearningMaterial?> FindByKeyAsync(LearningMaterialScope s, Guid key, CancellationToken ct) => Own(s).SingleOrDefaultAsync(x => x.ClientId == key, ct);
    public void Bind(LearningMaterialScope s, LearningMaterial p) => (db.LearningMaterialScope ?? throw new InvalidOperationException("LearningMaterialWriteDenied")).Bind(s, p);
    public void Add(LearningMaterialScope s, LearningMaterial p) { Bind(s, p); db.LearningMaterials.Add(p); }
    public async Task<T> ExecuteAsync<T>(LearningMaterialScope s, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (db.LearningMaterialScope is not null || db.LessonPlanScope is not null || db.AssignmentScope is not null || db.EnrolmentScope is not null || db.CalendarScope is not null || db.AcademicScope is not null || db.ClassScope is not null || db.CourseUnitScope is not null || db.SupportScope is not null || db.OnboardingScope is not null) throw new InvalidOperationException("LearningMaterialWriteDenied");
        await using var tx = await db.Database.BeginTransactionAsync(ct); db.LearningMaterialScope = new(db, s);
        try { var result = await action(ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result; }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new LearningMaterialException("VersionConflict"); }
        catch (DbUpdateException e) when (e.InnerException is MySql.Data.MySqlClient.MySqlException { Number: 1062 or 1213 or 1205 } || e.InnerException?.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException") { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new LearningMaterialException("VersionConflict"); }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        finally { db.LearningMaterialScope = null; }
    }
}
