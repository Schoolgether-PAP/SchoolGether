using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MySql.Data.MySqlClient;
using SchoolGether.Application.Academic;
using SchoolGether.Domain.Academic;
using SchoolGether.Domain.Audit;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Academic;

internal sealed class CourseUnitConfiguration : IEntityTypeConfiguration<CourseUnit>
{
    public void Configure(EntityTypeBuilder<CourseUnit> b)
    {
        b.ToTable("academic_course_unit"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(30); b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Syllabus).HasMaxLength(5000); b.Property(x => x.CreationFingerprint).HasMaxLength(64);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => new { x.InstitutionId, x.CourseId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.InstitutionId, x.ClientId }).IsUnique();
        b.HasOne<Course>().WithMany().HasForeignKey(x => new { x.InstitutionId, x.CourseId }).HasPrincipalKey(x => new { x.InstitutionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        foreach (var p in b.Metadata.GetProperties())
        {
            p.SetColumnName(string.Concat(p.Name.Select((ch, i) => char.IsUpper(ch) && i > 0 ? "_" + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString())));
            if (p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)) p.SetValueConverter(new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
        }
    }
}

// Autoriza apenas estas duas referências, no tenant, operação e transação originais.
internal sealed class CourseUnitWriteScope(SchoolGetherDbContext db, AuthorizedCourseUnitScope authorized)
{
    private readonly Guid transaction = db.Database.CurrentTransaction?.TransactionId ?? throw new InvalidOperationException("CourseUnitTransactionRequired");
    private Course? course;
    private CourseUnit? unit;
    private Guid clientId;
    private int createdBy;
    private string? fingerprint;
    private static InvalidOperationException Denied() => new("CourseUnitWriteDenied");
    internal void Bind(AuthorizedCourseUnitScope scope, Course parent, CourseUnit value)
    {
        if (!ReferenceEquals(scope, authorized) || db.Database.CurrentTransaction?.TransactionId != transaction || scope.Operation == "read" ||
            parent.InstitutionId != scope.InstitutionId || parent.Id != scope.CourseId || value.InstitutionId != scope.InstitutionId || value.CourseId != scope.CourseId ||
            (scope.UnitId.HasValue && value.Id != scope.UnitId) || (course is not null && !ReferenceEquals(course, parent)) || (unit is not null && !ReferenceEquals(unit, value))) throw Denied();
        if (unit is null) { clientId = value.ClientId; createdBy = value.CreatedBy; fingerprint = value.CreationFingerprint; }
        course = parent; unit = value;
    }
    internal bool Allows(EntityEntry entry)
    {
        if (course is null || unit is null || db.Database.CurrentTransaction?.TransactionId != transaction ||
            unit.ClientId != clientId || unit.CreatedBy != createdBy || unit.CreationFingerprint != fingerprint || course.InstitutionId != authorized.InstitutionId || course.Id != authorized.CourseId || unit.InstitutionId != authorized.InstitutionId || unit.CourseId != authorized.CourseId ||
            (authorized.UnitId.HasValue && unit.Id != authorized.UnitId)) return false;
        if (entry.Entity is AuditEntry a) return entry.State == EntityState.Added && a.ActorId == authorized.ActorId && a.InstitutionId == authorized.InstitutionId &&
            a.Action == "academic.unit." + authorized.Operation && a.Target == "academic.unit:" + unit.ClientId.ToString("D") && a.Result == "success";
        if (ReferenceEquals(entry.Entity, course))
            return entry.State == EntityState.Modified && course.IsActive && (bool)entry.Property("IsActive").OriginalValue! &&
                (long)entry.Property("RowVersion").OriginalValue! + 1 == course.RowVersion && course.UpdatedAt?.Kind == DateTimeKind.Utc &&
                entry.Properties.Where(x => x.IsModified).All(x => x.Metadata.Name is "RowVersion" or "UpdatedAt");
        if (!ReferenceEquals(entry.Entity, unit)) return false;
        // Toda a escrita de unidade tem de serializar também o pai ativo.
        var parentEntry = db.Entry(course);
        if (parentEntry.State != EntityState.Modified || !Allows(parentEntry)) return false;
        if (entry.State == EntityState.Added) return authorized.Operation == "create" && authorized.UnitId is null && unit.Id == 0 && unit.CreatedBy == authorized.ActorId && unit.RowVersion == 1 && unit.IsActive && unit.DeletedAt is null && unit.CreatedAt.Kind == DateTimeKind.Utc;
        if (entry.State != EntityState.Modified || authorized.Operation is not ("update" or "archive" or "restore") ||
            (int)entry.Property("InstitutionId").OriginalValue! != authorized.InstitutionId || (int)entry.Property("CourseId").OriginalValue! != authorized.CourseId ||
            (long)entry.Property("RowVersion").OriginalValue! + 1 != unit.RowVersion || unit.UpdatedAt?.Kind != DateTimeKind.Utc) return false;
        var allowed = authorized.Operation == "update" ? new[] { "Code", "Name", "Syllabus", "RowVersion", "UpdatedAt" } : new[] { "IsActive", "DeletedAt", "RowVersion", "UpdatedAt" };
        if (authorized.Operation == "update" && (!(bool)entry.Property("IsActive").OriginalValue! || !unit.IsActive)) return false;
        if (authorized.Operation == "archive" && (!(bool)entry.Property("IsActive").OriginalValue! || unit.IsActive || unit.DeletedAt is null)) return false;
        if (authorized.Operation == "restore" && ((bool)entry.Property("IsActive").OriginalValue! || !unit.IsActive || unit.DeletedAt is not null)) return false;
        return entry.Properties.Where(x => x.IsModified).All(x => allowed.Contains(x.Metadata.Name));
    }
}
internal sealed class AcademicCourseUnitStore(SchoolGetherDbContext db) : IAcademicCourseUnitStore
{
    private IQueryable<CourseUnit> Query(AuthorizedCourseUnitScope scope) => db.CourseUnits.Where(x => x.InstitutionId == scope.InstitutionId);
    public Task<Course?> FindCourseAsync(AuthorizedCourseUnitScope scope, CancellationToken ct) => db.Courses.SingleOrDefaultAsync(x => x.Id == scope.CourseId && x.InstitutionId == scope.InstitutionId, ct);
    public Task<CourseUnit?> FindAsync(AuthorizedCourseUnitScope scope, int id, CancellationToken ct) => Query(scope).SingleOrDefaultAsync(x => x.Id == id && x.CourseId == scope.CourseId, ct);
    public Task<CourseUnit?> FindByClientIdAsync(AuthorizedCourseUnitScope scope, Guid clientId, CancellationToken ct) => Query(scope).SingleOrDefaultAsync(x => x.ClientId == clientId, ct);
    public async Task<CourseUnitPage> ListAsync(AuthorizedCourseUnitScope scope, CourseUnitQuery query, CancellationToken ct)
    {
        var rows = Query(scope).AsNoTracking().Where(x => x.CourseId == scope.CourseId && x.IsActive == (query.Status == "Active"));
        if (!string.IsNullOrWhiteSpace(query.Search)) { var search = query.Search.Trim().ToUpperInvariant(); rows = rows.Where(x => x.Code.Contains(search) || x.Name.ToUpper().Contains(search)); }
        var total = await rows.CountAsync(ct); var items = await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(items.Select(x => CourseUnitResult.From(x)).ToArray(), total, query.Page, query.PageSize);
    }
    public void Bind(AuthorizedCourseUnitScope scope, Course course, CourseUnit unit) => (db.CourseUnitScope ?? throw new InvalidOperationException("CourseUnitWriteDenied")).Bind(scope, course, unit);
    public void Add(AuthorizedCourseUnitScope scope, Course course, CourseUnit unit) { Bind(scope, course, unit); db.CourseUnits.Add(unit); }
}
internal sealed class CourseUnitOfWork(SchoolGetherDbContext db) : ICourseUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(AuthorizedCourseUnitScope scope, Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        if (db.CourseUnitScope is not null || db.AcademicScope is not null || db.SupportScope is not null || db.OnboardingScope is not null) throw new InvalidOperationException("CourseUnitWriteDenied");
        await using var transaction = await db.Database.BeginTransactionAsync(ct); db.CourseUnitScope = new(db, scope);
        try { var result = await operation(ct); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result; }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new AcademicException("VersionConflict"); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 or 1213 or 1205 } || db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" && ex.InnerException?.Message.Contains("UNIQUE constraint failed:", StringComparison.Ordinal) == true)
        { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new AcademicException("CourseUnitConflict"); }
        catch { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        finally { db.CourseUnitScope = null; }
    }
}
