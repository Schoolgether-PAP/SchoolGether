using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MySql.Data.MySqlClient;
using SchoolGether.Application.Academic;
using SchoolGether.Domain.Academic;
using SchoolGether.Domain.Audit;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Academic;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("academic_course"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(30); b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(1000); b.Property(x => x.CreationFingerprint).HasMaxLength(64);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => new { x.InstitutionId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.InstitutionId, x.ClientId }).IsUnique();
        b.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        foreach (var p in b.Metadata.GetProperties())
        {
            p.SetColumnName(string.Concat(p.Name.Select((ch, i) => char.IsUpper(ch) && i > 0 ? "_" + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString())));
            if (p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
                p.SetValueConverter(new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
        }
    }
}

// A capacidade só autoriza uma referência, durante a transação que a criou.
internal sealed class AcademicWriteScope(SchoolGetherDbContext db, AuthorizedAcademicScope authorized)
{
    private readonly Guid transaction = db.Database.CurrentTransaction?.TransactionId ?? throw new InvalidOperationException("AcademicTransactionRequired");
    private Course? course;
    internal void Require(AuthorizedAcademicScope scope)
    {
        if (!ReferenceEquals(authorized, scope) || db.Database.CurrentTransaction?.TransactionId != transaction)
            throw new InvalidOperationException("AcademicWriteDenied");
    }
    internal void Bind(AuthorizedAcademicScope scope, Course value)
    {
        Require(scope);
        if (authorized.Operation == "read" || value.InstitutionId != scope.InstitutionId ||
            (scope.CourseId.HasValue && value.Id != scope.CourseId) || (course is not null && !ReferenceEquals(course, value)))
            throw new InvalidOperationException("AcademicWriteDenied");
        course = value;
    }
    internal bool Allows(EntityEntry entry)
    {
        if (course is null || db.Database.CurrentTransaction?.TransactionId != transaction) return false;
        if (entry.Entity is AuditEntry a) return entry.State == EntityState.Added && a.ActorId == authorized.ActorId &&
            a.InstitutionId == authorized.InstitutionId && a.Action == "academic." + authorized.Operation &&
            a.Target == "academic.course:" + course.ClientId.ToString("D") && a.Result == "success";
        if (!ReferenceEquals(entry.Entity, course) || course.InstitutionId != authorized.InstitutionId) return false;
        if (entry.State == EntityState.Added) return authorized.Operation == "create" && course.CreatedBy == authorized.ActorId &&
            course.Id == 0 && course.RowVersion == 1 && course.IsActive && course.DeletedAt is null;
        if (entry.State != EntityState.Modified || authorized.Operation is not ("update" or "archive" or "restore")) return false;
        if (entry.Property("InstitutionId").OriginalValue is not int tenant || tenant != authorized.InstitutionId ||
            (long)entry.Property("RowVersion").OriginalValue! + 1 != course.RowVersion) return false;
        var allowed = authorized.Operation == "update" ? new[] { "Code", "Name", "Description", "RowVersion", "UpdatedAt" } :
            new[] { "IsActive", "DeletedAt", "RowVersion", "UpdatedAt" };
        if (authorized.Operation == "update" && (!(bool)entry.Property("IsActive").OriginalValue! || !course.IsActive)) return false;
        if (authorized.Operation == "archive" && (!(bool)entry.Property("IsActive").OriginalValue! || course.IsActive || course.DeletedAt is null)) return false;
        if (authorized.Operation == "restore" && ((bool)entry.Property("IsActive").OriginalValue! || !course.IsActive || course.DeletedAt is not null)) return false;
        return entry.Properties.Where(x => x.IsModified).All(x => allowed.Contains(x.Metadata.Name));
    }
}

internal sealed class AcademicCourseStore(SchoolGetherDbContext db) : IAcademicCourseStore
{
    private IQueryable<Course> Query(AuthorizedAcademicScope scope) => db.Courses.Where(x => x.InstitutionId == scope.InstitutionId);
    public async Task<CoursePage> ListAsync(AuthorizedAcademicScope scope, CourseQuery query, CancellationToken ct)
    {
        var rows = Query(scope).AsNoTracking().Where(x => x.IsActive == (query.Status == "Active"));
        if (!string.IsNullOrWhiteSpace(query.Search))
        { var search = query.Search.Trim().ToUpperInvariant(); rows = rows.Where(x => x.Code.Contains(search) || x.Name.ToUpper().Contains(search)); }
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(items.Select(x => CourseResult.From(x)).ToArray(), total, query.Page, query.PageSize);
    }
    public Task<Course?> FindAsync(AuthorizedAcademicScope scope, int id, CancellationToken ct) => Query(scope).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Course?> FindByClientIdAsync(AuthorizedAcademicScope scope, Guid clientId, CancellationToken ct) => Query(scope).SingleOrDefaultAsync(x => x.ClientId == clientId, ct);
    public void Bind(AuthorizedAcademicScope scope, Course value) => (db.AcademicScope ?? throw new InvalidOperationException("AcademicWriteDenied")).Bind(scope, value);
    public void Add(AuthorizedAcademicScope scope, Course value) { Bind(scope, value); db.Courses.Add(value); }
}

internal sealed class AcademicUnitOfWork(SchoolGetherDbContext db) : IAcademicUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(AuthorizedAcademicScope scope, Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        if (db.ClassScope is not null || db.CourseUnitScope is not null || db.AcademicScope is not null || db.SupportScope is not null || db.OnboardingScope is not null)
            throw new InvalidOperationException("AcademicWriteDenied");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.AcademicScope = new(db, scope);
        try
        {
            var result = await operation(ct); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
        }
        catch (DbUpdateConcurrencyException)
        { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new AcademicException("VersionConflict"); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 or 1213 or 1205 } ||
            db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" && ex.InnerException?.Message.Contains("UNIQUE constraint failed:", StringComparison.Ordinal) == true)
        { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new AcademicException("CourseConflict"); }
        catch { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        finally { db.AcademicScope = null; }
    }
}
