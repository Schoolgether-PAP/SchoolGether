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

internal static class ClassColumns
{
    internal static void Configure<T>(EntityTypeBuilder<T> b) where T : class
    {
        foreach (var p in b.Metadata.GetProperties())
        {
            p.SetColumnName(string.Concat(p.Name.Select((ch, i) => char.IsUpper(ch) && i > 0 ? "_" + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString())));
            if (p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)) p.SetValueConverter(new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
        }
    }
}
internal sealed class AcademicClassConfiguration : IEntityTypeConfiguration<AcademicClass>
{
    public void Configure(EntityTypeBuilder<AcademicClass> b)
    {
        b.ToTable("academic_class"); b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.InstitutionId, x.CourseId, x.Id });
        b.Property(x => x.Code).HasMaxLength(30); b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.AcademicYear).HasMaxLength(9); b.Property(x => x.CreationFingerprint).HasMaxLength(64);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => new { x.InstitutionId, x.CourseId, x.StudyYear, x.AcademicYear, x.Code }).IsUnique();
        b.HasIndex(x => new { x.InstitutionId, x.ClientId }).IsUnique();
        b.HasOne<Course>().WithMany().HasForeignKey(x => new { x.InstitutionId, x.CourseId }).HasPrincipalKey(x => new { x.InstitutionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        ClassColumns.Configure(b);
    }
}
internal sealed class AcademicClassUnitConfiguration : IEntityTypeConfiguration<AcademicClassUnit>
{
    public void Configure(EntityTypeBuilder<AcademicClassUnit> b)
    {
        b.ToTable("academic_class_unit"); b.HasKey(x => x.Id); b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => new { x.ClassId, x.UnitId }).IsUnique();
        b.HasOne(x => x.Class).WithMany().HasForeignKey(x => new { x.InstitutionId, x.CourseId, x.ClassId }).HasPrincipalKey(x => new { x.InstitutionId, x.CourseId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => new { x.InstitutionId, x.CourseId, x.UnitId }).HasPrincipalKey(x => new { x.InstitutionId, x.CourseId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        ClassColumns.Configure(b);
    }
}

// Uma capacidade de agregado: referências exatas e identidades imutáveis, incluindo chaves EF temporárias.
internal sealed class ClassWriteScope(SchoolGetherDbContext db, AuthorizedClassScope authorized)
{
    private readonly Guid transaction = db.Database.CurrentTransaction?.TransactionId ?? throw new InvalidOperationException("ClassTransactionRequired");
    private Course? parent;
    private AcademicClass? value;
    private Dictionary<string, object?>? parentIdentity;
    private Dictionary<string, object?>? classIdentity;
    private long parentVersion;
    private long classVersion;
    private bool classWasActive;
    private readonly Dictionary<AcademicClassUnit, Dictionary<string, object?>> links = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<CourseUnit, Dictionary<string, object?>> units = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<AcademicClassUnit> originallySelected = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<AcademicClassUnit, long> linkVersions = new(ReferenceEqualityComparer.Instance);
    private static InvalidOperationException Denied() => new("ClassWriteDenied");
    private static Dictionary<string, object?> Capture(EntityEntry entry, params string[] names) => entry.Properties.Where(p => names.Contains(p.Metadata.Name)).ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
    private static bool Matches(EntityEntry entry, Dictionary<string, object?> snapshot) => snapshot.All(p => Equals(entry.Property(p.Key).CurrentValue, p.Value));
    private void Require(AuthorizedClassScope scope)
    { if (!ReferenceEquals(scope, authorized) || db.Database.CurrentTransaction?.TransactionId != transaction || scope.Operation == "read") throw Denied(); }
    internal void Bind(AuthorizedClassScope scope, Course course, AcademicClass entity, IReadOnlyList<AcademicClassUnit> history, IReadOnlyList<CourseUnit> eligible)
    {
        Require(scope);
        if (parent is not null || value is not null || course.Id != scope.CourseId || course.InstitutionId != scope.InstitutionId || entity.InstitutionId != scope.InstitutionId || entity.CourseId != scope.CourseId || scope.ClassId.HasValue && scope.ClassId != entity.Id || scope.Operation == "create" && db.Entry(entity).State != EntityState.Detached) throw Denied();
        parent = course; value = entity;
        parentVersion = course.RowVersion; classVersion = entity.RowVersion; classWasActive = entity.IsActive;
        parentIdentity = Capture(db.Entry(course), "Id", "InstitutionId", "ClientId", "CreatedBy", "CreatedAt", "CreationFingerprint", "Code", "Name", "Description", "IsActive", "DeletedAt");
        classIdentity = Capture(db.Entry(entity), "Id", "InstitutionId", "CourseId", "ClientId", "CreatedBy", "CreatedAt", "CreationFingerprint", "StudyYear", "AcademicYear");
        foreach (var link in history)
        {
            if (!ReferenceEquals(link.Class, entity) || link.ClassId != entity.Id || link.CourseId != scope.CourseId || link.InstitutionId != scope.InstitutionId || db.Entry(link).State != EntityState.Unchanged) throw Denied();
            CaptureLink(link); if (link.IsActive) originallySelected.Add(link);
        }
        foreach (var unit in eligible.Concat(history.Select(l => l.Unit)).Distinct())
        {
            if (unit.InstitutionId != scope.InstitutionId || unit.CourseId != scope.CourseId || unit.StudyYear != entity.StudyYear || db.Entry(unit).State != EntityState.Unchanged) throw Denied();
            units.Add(unit, db.Entry(unit).Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
        }
        if (eligible.Any(u => !u.IsActive && !originallySelected.Any(l => ReferenceEquals(l.Unit, u)))) throw Denied();
    }
    internal void Add(AuthorizedClassScope scope, AcademicClass entity)
    {
        Require(scope); if (scope.Operation != "create" || !ReferenceEquals(value, entity) || entity.Id != 0 || classIdentity is null || !Matches(db.Entry(entity), classIdentity)) throw Denied();
        db.AcademicClasses.Add(entity);
        if (!db.Entry(entity).Property(x => x.Id).IsTemporary) throw Denied();
        classIdentity["Id"] = db.Entry(entity).Property(x => x.Id).CurrentValue;
    }
    internal void AddLink(AuthorizedClassScope scope, AcademicClassUnit link)
    {
        Require(scope);
        if (value is null || !ReferenceEquals(link.Class, value) || !units.ContainsKey(link.Unit) || !link.Unit.IsActive || links.Keys.Any(l => ReferenceEquals(l.Unit, link.Unit)) || link.CreatedBy != scope.ActorId || scope.Operation is not ("create" or "update")) throw Denied();
        db.ClassUnits.Add(link); if (!db.Entry(link).Property(x => x.Id).IsTemporary) throw Denied(); CaptureLink(link);
    }
    private void CaptureLink(AcademicClassUnit link)
    {
        links.Add(link, Capture(db.Entry(link), "Id", "InstitutionId", "CourseId", "ClassId", "UnitId", "CreatedBy", "CreatedAt"));
        linkVersions.Add(link, link.RowVersion);
    }
    private bool ValidAggregate()
    {
        if (parent is null || value is null || classIdentity is null || parentIdentity is null || db.Database.CurrentTransaction?.TransactionId != transaction || !Matches(db.Entry(parent), parentIdentity) || !Matches(db.Entry(value), classIdentity) || !ValidClass(db.Entry(value))) return false;
        if (units.Any(u => !Matches(db.Entry(u.Key), u.Value) || db.Entry(u.Key).State != EntityState.Unchanged)) return false;
        var selected = links.Keys.Where(l => l.IsActive).ToArray();
        if (selected.Length is < 1 or > 100 || selected.Select(l => l.UnitId).Distinct().Count() != selected.Length) return false;
        return links.All(l => Matches(db.Entry(l.Key), l.Value) && ReferenceEquals(l.Key.Class, value) && units.ContainsKey(l.Key.Unit) &&
            l.Key.UnitId == l.Key.Unit.Id && l.Key.InstitutionId == authorized.InstitutionId && l.Key.CourseId == authorized.CourseId &&
            l.Key.Unit.StudyYear == value.StudyYear && (!l.Key.IsActive || l.Key.Unit.IsActive || originallySelected.Contains(l.Key)));
    }
    private bool ValidClass(EntityEntry entry)
    {
        var entity = value!;
        if (entry.State == EntityState.Added) return authorized.Operation == "create" && authorized.ClassId is null && entry.Property("Id").IsTemporary && entity.CreatedBy == authorized.ActorId && entity.RowVersion == 1 && entity.IsActive && entity.DeletedAt is null && entity.CreatedAt.Kind == DateTimeKind.Utc;
        if (entry.State != EntityState.Modified || (long)entry.Property("RowVersion").OriginalValue! != classVersion || classVersion + 1 != entity.RowVersion || entity.UpdatedAt?.Kind != DateTimeKind.Utc || (bool)entry.Property("IsActive").OriginalValue! != classWasActive) return false;
        var allowed = authorized.Operation == "update" ? new[] { "Code", "Name", "RowVersion", "UpdatedAt" } : new[] { "IsActive", "DeletedAt", "RowVersion", "UpdatedAt" };
        if (authorized.Operation == "update" && (!classWasActive || !entity.IsActive)) return false;
        if (authorized.Operation == "archive" && (!classWasActive || entity.IsActive || entity.DeletedAt?.Kind != DateTimeKind.Utc)) return false;
        if (authorized.Operation == "restore" && (classWasActive || !entity.IsActive || entity.DeletedAt is not null)) return false;
        return authorized.Operation is "update" or "archive" or "restore" && entry.Properties.Where(p => p.IsModified).All(p => allowed.Contains(p.Metadata.Name));
    }
    internal bool Allows(EntityEntry entry)
    {
        if (!ValidAggregate()) return false;
        var course = parent!; var entity = value!;
        var parentEntry = db.Entry(course);
        var parentValid = parentEntry.State == EntityState.Modified && course.IsActive && (bool)parentEntry.Property("IsActive").OriginalValue! && (long)parentEntry.Property("RowVersion").OriginalValue! == parentVersion && parentVersion + 1 == course.RowVersion && course.UpdatedAt?.Kind == DateTimeKind.Utc && parentEntry.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name is "UpdatedAt" or "RowVersion");
        if (!parentValid) return false;
        if (ReferenceEquals(entry.Entity, course)) return true;
        if (entry.Entity is AuditEntry audit) return entry.State == EntityState.Added && audit.ActorId == authorized.ActorId && audit.InstitutionId == authorized.InstitutionId && audit.Action == "academic.class." + authorized.Operation && audit.Target == "academic.class:" + entity.ClientId.ToString("D") && audit.Result == "success";
        if (ReferenceEquals(entry.Entity, entity)) return true;
        if (entry.Entity is not AcademicClassUnit link || !links.ContainsKey(link) || authorized.Operation is not ("create" or "update")) return false;
        if (entry.State == EntityState.Added) return entry.Property("Id").IsTemporary && link.RowVersion == 1 && link.IsActive && link.DeletedAt is null && link.CreatedBy == authorized.ActorId && link.CreatedAt.Kind == DateTimeKind.Utc;
        return entry.State == EntityState.Modified && (long)entry.Property("RowVersion").OriginalValue! == linkVersions[link] && linkVersions[link] + 1 == link.RowVersion && (bool)entry.Property("IsActive").OriginalValue! == originallySelected.Contains(link) && link.IsActive != originallySelected.Contains(link) && link.UpdatedAt?.Kind == DateTimeKind.Utc && (link.IsActive ? link.DeletedAt is null : link.DeletedAt?.Kind == DateTimeKind.Utc) && entry.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name is "IsActive" or "DeletedAt" or "RowVersion" or "UpdatedAt");
    }
}

internal sealed class AcademicClassStore(SchoolGetherDbContext db) : IAcademicClassStore
{
    private IQueryable<AcademicClass> Query(AuthorizedClassScope scope) => db.AcademicClasses.Where(x => x.InstitutionId == scope.InstitutionId);
    public Task<Course?> FindCourseAsync(AuthorizedClassScope scope, CancellationToken ct) => db.Courses.SingleOrDefaultAsync(x => x.Id == scope.CourseId && x.InstitutionId == scope.InstitutionId, ct);
    public Task<AcademicClass?> FindAsync(AuthorizedClassScope scope, int id, CancellationToken ct) => Query(scope).SingleOrDefaultAsync(x => x.Id == id && x.CourseId == scope.CourseId, ct);
    public Task<AcademicClass?> FindByClientIdAsync(AuthorizedClassScope scope, Guid clientId, CancellationToken ct) => Query(scope).SingleOrDefaultAsync(x => x.ClientId == clientId, ct);
    public Task<List<AcademicClassUnit>> LinksAsync(AuthorizedClassScope scope, AcademicClass value, CancellationToken ct) => db.ClassUnits.Include(x => x.Unit).Where(x => x.InstitutionId == scope.InstitutionId && x.CourseId == value.CourseId && x.ClassId == value.Id).ToListAsync(ct);
    public Task<List<CourseUnit>> UnitsAsync(AuthorizedClassScope scope, IReadOnlyList<int> ids, CancellationToken ct) => db.CourseUnits.Where(x => x.InstitutionId == scope.InstitutionId && x.CourseId == scope.CourseId && ids.Contains(x.Id)).ToListAsync(ct);
    public async Task<AcademicClassPage> ListAsync(AuthorizedClassScope scope, AcademicClassQuery query, CancellationToken ct)
    {
        var rows = Query(scope).AsNoTracking().Where(x => x.CourseId == scope.CourseId && x.IsActive == (query.Status == "Active"));
        if (query.StudyYear.HasValue) rows = rows.Where(x => x.StudyYear == query.StudyYear);
        if (query.AcademicYear is not null) rows = rows.Where(x => x.AcademicYear == query.AcademicYear);
        if (!string.IsNullOrWhiteSpace(query.Search)) { var search = query.Search.Trim().ToUpperInvariant(); rows = rows.Where(x => x.Code.Contains(search) || x.Name.ToUpper().Contains(search)); }
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(x => new AcademicClassListItem(x.Id, x.InstitutionId, x.CourseId, x.ClientId, x.Code, x.Name, x.StudyYear, x.AcademicYear, x.IsActive, x.RowVersion, x.CreatedAt, x.UpdatedAt, db.ClassUnits.Count(l => l.ClassId == x.Id && l.IsActive))).ToArrayAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }
    public void Bind(AuthorizedClassScope scope, Course parent, AcademicClass value, IReadOnlyList<AcademicClassUnit> links, IReadOnlyList<CourseUnit> eligible) => (db.ClassScope ?? throw new InvalidOperationException("ClassWriteDenied")).Bind(scope, parent, value, links, eligible);
    public void Add(AuthorizedClassScope scope, AcademicClass value) => (db.ClassScope ?? throw new InvalidOperationException("ClassWriteDenied")).Add(scope, value);
    public void AddLink(AuthorizedClassScope scope, AcademicClassUnit link) => (db.ClassScope ?? throw new InvalidOperationException("ClassWriteDenied")).AddLink(scope, link);
}
internal sealed class ClassUnitOfWork(SchoolGetherDbContext db) : IClassUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(AuthorizedClassScope scope, Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        if (db.ClassScope is not null || db.CourseUnitScope is not null || db.AcademicScope is not null || db.SupportScope is not null || db.OnboardingScope is not null) throw new InvalidOperationException("ClassWriteDenied");
        await using var transaction = await db.Database.BeginTransactionAsync(ct); db.ClassScope = new(db, scope);
        try { var result = await operation(ct); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result; }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new AcademicException("VersionConflict"); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 or 1213 or 1205 } || db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" && ex.InnerException?.Message.Contains("UNIQUE constraint failed:", StringComparison.Ordinal) == true)
        { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new AcademicException("ClassConflict"); }
        catch { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        finally { db.ClassScope = null; }
    }
}
