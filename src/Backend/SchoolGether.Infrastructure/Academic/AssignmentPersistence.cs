using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySql.Data.MySqlClient;
using SchoolGether.Application.Academic;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Academic;
using SchoolGether.Domain.Identity;
using SchoolGether.Domain.Audit;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Infrastructure.Academic;

internal sealed class AcademicAssignmentConfiguration:IEntityTypeConfiguration<AcademicAssignment>
{
    public void Configure(EntityTypeBuilder<AcademicAssignment> b)
    {
        b.ToTable("academic_assignment",t=>t.HasCheckConstraint("ck_assignment_active_slot","(is_active = 1 AND active_teacher_id IS NOT NULL AND active_teacher_id = teacher_id) OR (is_active = 0 AND active_teacher_id IS NULL)"));
        b.HasKey(x=>x.Id);b.Property(x=>x.AcademicYear).HasMaxLength(9);b.Property(x=>x.CreationFingerprint).HasMaxLength(64);b.Property(x=>x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x=>new{x.InstitutionId,x.ClientId}).IsUnique();b.HasIndex(x=>new{x.InstitutionId,x.ClassId,x.UnitId,x.TeacherId}).IsUnique();
        b.HasOne<CourseUnit>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.CourseId,x.UnitId}).HasPrincipalKey(x=>new{x.InstitutionId,x.CourseId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademicClass>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.CourseId,x.StudyYear,x.AcademicYear,x.ClassId}).HasPrincipalKey(x=>new{x.InstitutionId,x.CourseId,x.StudyYear,x.AcademicYear,x.Id}).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InstitutionMember>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.TeacherId}).HasPrincipalKey(x=>new{x.InstitutionId,x.UserId}).OnDelete(DeleteBehavior.Restrict);
        ClassColumns.Configure(b);
    }
}

// Capacidade de uma única transição. As referências e os valores originais são vinculados antes de qualquer mutação.
internal sealed class AssignmentWriteScope(SchoolGetherDbContext db,AuthorizedAssignmentScope authorized)
{
    private readonly Guid transaction=db.Database.CurrentTransaction?.TransactionId??throw new InvalidOperationException("AssignmentTransactionRequired");
    private AcademicClassUnit? selected;
    internal void Select(AuthorizedAssignmentScope scope,AcademicClassUnit link)
    {
        Require(scope);
        if(link.InstitutionId!=scope.InstitutionId||link.CourseId!=scope.CourseId||link.ClassId!=scope.ClassId||!link.IsActive||!link.Unit.IsActive||link.Unit.StudyYear!=link.Class.StudyYear)throw Denied();
        selected=link;
    }
    private Course? course;private AcademicClass? value;private AcademicAssignment? assignment;
    private Dictionary<string,object?>? courseBefore,classBefore,assignmentBefore;
    private static InvalidOperationException Denied()=>new("AssignmentWriteDenied");
    private static Dictionary<string,object?> Capture(EntityEntry entry)=>entry.Properties.ToDictionary(x=>x.Metadata.Name,x=>x.CurrentValue);
    private static bool OriginalMatches(EntityEntry entry,Dictionary<string,object?> before)=>before.All(x=>Equals(entry.Property(x.Key).OriginalValue,x.Value));
    private static bool ImmutableMatches(EntityEntry entry,Dictionary<string,object?> before,params string[] allowed)=>before.Where(x=>!allowed.Contains(x.Key)).All(x=>Equals(entry.Property(x.Key).CurrentValue,x.Value));
    private void Require(AuthorizedAssignmentScope scope)
    {if(!ReferenceEquals(scope,authorized)||db.Database.CurrentTransaction?.TransactionId!=transaction||scope.Operation is not("create" or "cancel" or "restore"))throw Denied();}
    internal void Bind(AuthorizedAssignmentScope scope,Course parent,AcademicClass group,AcademicAssignment entity,TeacherDirectoryItem? teacher)
    {
        Require(scope);
        if(course is not null||db.Entry(parent).State!=EntityState.Unchanged||db.Entry(group).State!=EntityState.Unchanged||parent.Id!=scope.CourseId||parent.InstitutionId!=scope.InstitutionId||group.Id!=scope.ClassId||group.CourseId!=parent.Id||group.InstitutionId!=scope.InstitutionId||entity.InstitutionId!=scope.InstitutionId||entity.CourseId!=parent.Id||entity.ClassId!=group.Id||entity.StudyYear!=group.StudyYear||entity.AcademicYear!=group.AcademicYear||scope.AssignmentId.HasValue&&scope.AssignmentId!=entity.Id)throw Denied();
        if(scope.Operation=="create"?(scope.AssignmentId is not null||db.Entry(entity).State!=EntityState.Detached||entity.Id!=0||entity.CreatedBy!=scope.ActorId):db.Entry(entity).State!=EntityState.Unchanged||scope.AssignmentId!=entity.Id)throw Denied();
        if(scope.Operation!="cancel"&&(!parent.IsActive||!group.IsActive||teacher is not{Available:true}||teacher.UserId!=entity.TeacherId))throw Denied();
        course=parent;value=group;assignment=entity;courseBefore=Capture(db.Entry(parent));classBefore=Capture(db.Entry(group));assignmentBefore=Capture(db.Entry(entity));
    }
    internal void Add(AuthorizedAssignmentScope scope,AcademicAssignment entity)
    {
        Require(scope);if(scope.Operation!="create"||!ReferenceEquals(entity,assignment)||assignmentBefore is null||!ImmutableMatches(db.Entry(entity),assignmentBefore))throw Denied();
        db.Assignments.Add(entity);if(!db.Entry(entity).Property(x=>x.Id).IsTemporary)throw Denied();assignmentBefore["Id"]=db.Entry(entity).Property(x=>x.Id).CurrentValue;
    }
    private bool Metadata(EntityEntry entry,Dictionary<string,object?> before,bool touched)
    {
        if(!OriginalMatches(entry,before))return false;
        if(!touched)return entry.State==EntityState.Unchanged&&ImmutableMatches(entry,before);
        return entry.State==EntityState.Modified&&ImmutableMatches(entry,before,"UpdatedAt","RowVersion")&&(long)entry.Property("RowVersion").CurrentValue! == (long)before["RowVersion"]!+1&&entry.Property("UpdatedAt").CurrentValue is DateTime{Kind:DateTimeKind.Utc}&&entry.Properties.Where(x=>x.IsModified).All(x=>x.Metadata.Name is "UpdatedAt" or "RowVersion");
    }
    private bool Valid()
    {
        if(course is null||value is null||assignment is null||courseBefore is null||classBefore is null||assignmentBefore is null||db.Database.CurrentTransaction?.TransactionId!=transaction)return false;
        if(!Metadata(db.Entry(course),courseBefore,authorized.Operation!="cancel")||!Metadata(db.Entry(value),classBefore,true))return false;
        if(authorized.Operation!="cancel"&&(!course.IsActive||!value.IsActive||selected is null||selected.UnitId!=assignment.UnitId||!selected.IsActive||!selected.Unit.IsActive||selected.Unit.StudyYear!=value.StudyYear||db.Entry(selected).State!=EntityState.Unchanged||db.Entry(selected.Unit).State!=EntityState.Unchanged))return false;
        var entry=db.Entry(assignment);
        if(!ImmutableMatches(entry,assignmentBefore,"IsActive","ActiveTeacherId","DeletedAt","UpdatedAt","RowVersion")||assignment.CreatedAt.Kind!=DateTimeKind.Utc||assignment.ClientId==Guid.Empty||assignment.TeacherId<=0||assignment.ActiveTeacherId!=(assignment.IsActive?assignment.TeacherId:null))return false;
        if(authorized.Operation=="create")
        {if(entry.State!=EntityState.Added||!entry.Property("Id").IsTemporary||!assignment.IsActive||assignment.RowVersion!=1||assignment.DeletedAt is not null||assignment.UpdatedAt is not null)return false;}
        else
        {
            if(entry.State!=EntityState.Modified||!OriginalMatches(entry,assignmentBefore)||assignment.RowVersion!=(long)assignmentBefore["RowVersion"]!+1||assignment.UpdatedAt?.Kind!=DateTimeKind.Utc||entry.Properties.Where(x=>x.IsModified).Any(x=>x.Metadata.Name is not("IsActive" or "ActiveTeacherId" or "DeletedAt" or "UpdatedAt" or "RowVersion")))return false;
            if(authorized.Operation=="cancel"?!(bool)assignmentBefore["IsActive"]!||assignment.IsActive||assignment.DeletedAt?.Kind!=DateTimeKind.Utc:(bool)assignmentBefore["IsActive"]!||!assignment.IsActive||assignment.DeletedAt is not null)return false;
        }
        return db.ChangeTracker.Entries<AuditEntry>().Count(x=>x.State==EntityState.Added)==1;
    }
    internal bool Allows(EntityEntry entry)
    {
        if(!Valid())return false;
        if(ReferenceEquals(entry.Entity,course))return authorized.Operation!="cancel";
        if(ReferenceEquals(entry.Entity,value)||ReferenceEquals(entry.Entity,assignment))return true;
        return entry.Entity is AuditEntry audit&&entry.State==EntityState.Added&&audit.ActorId==authorized.ActorId&&audit.ActorName==authorized.ActorName&&audit.InstitutionId==authorized.InstitutionId&&audit.Action=="academic.assignment."+authorized.Operation&&audit.Target=="academic.assignment:"+assignment!.ClientId.ToString("D")&&audit.Result=="success"&&audit.Reason=="Confirmed"&&audit.Before is null&&audit.After is null;
    }
}
internal sealed class AcademicAssignmentStore(SchoolGetherDbContext db):IAcademicAssignmentStore
{
    public async Task<IReadOnlyList<AssignmentUnitDescription>> DescribeUnitsAsync(AuthorizedAssignmentScope scope,IReadOnlyList<int> unitIds,CancellationToken ct)
        => await db.CourseUnits.AsNoTracking().Where(x=>x.InstitutionId==scope.InstitutionId&&x.CourseId==scope.CourseId&&unitIds.Contains(x.Id))
            .Select(x=>new AssignmentUnitDescription(x.Id,x.Code,x.Name)).ToArrayAsync(ct);
    public async Task<bool> IsSelectedUnitAsync(AuthorizedAssignmentScope s,int id,CancellationToken ct)
    {
        var link=await db.ClassUnits.Include(x=>x.Unit).Include(x=>x.Class).SingleOrDefaultAsync(x=>x.InstitutionId==s.InstitutionId&&x.CourseId==s.CourseId&&x.ClassId==s.ClassId&&x.UnitId==id&&x.IsActive&&x.Unit.IsActive&&x.Unit.StudyYear==x.Class.StudyYear,ct);
        if(link is null)return false;
        (db.AssignmentScope??throw new InvalidOperationException("AssignmentWriteDenied")).Select(s,link);return true;
    }
    private IQueryable<AcademicAssignment> Query(AuthorizedAssignmentScope scope)=>db.Assignments.Where(x=>x.InstitutionId==scope.InstitutionId);
    public Task<Course?> FindCourseAsync(AuthorizedAssignmentScope scope,CancellationToken ct)=>db.Courses.SingleOrDefaultAsync(x=>x.InstitutionId==scope.InstitutionId&&x.Id==scope.CourseId,ct);
    public Task<AcademicClass?> FindClassAsync(AuthorizedAssignmentScope scope,CancellationToken ct)=>db.AcademicClasses.SingleOrDefaultAsync(x=>x.InstitutionId==scope.InstitutionId&&x.CourseId==scope.CourseId&&x.Id==scope.ClassId,ct);
    public Task<AcademicAssignment?> FindAsync(AuthorizedAssignmentScope scope,int id,CancellationToken ct)=>Query(scope).SingleOrDefaultAsync(x=>x.Id==id&&x.CourseId==scope.CourseId&&x.ClassId==scope.ClassId,ct);
    public Task<AcademicAssignment?> FindByClientIdAsync(AuthorizedAssignmentScope scope,Guid clientId,CancellationToken ct)=>Query(scope).SingleOrDefaultAsync(x=>x.ClientId==clientId,ct);
    public async Task<AssignmentRows> ListAsync(AuthorizedAssignmentScope scope,AssignmentQuery query,IReadOnlyList<int>? teacherIds,CancellationToken ct)
    {
        var rows=Query(scope).AsNoTracking().Where(x=>x.CourseId==scope.CourseId&&x.ClassId==scope.ClassId&&x.IsActive==(query.Status=="Active"));
        if(teacherIds is not null)rows=rows.Where(x=>teacherIds.Contains(x.TeacherId));
        var total=await rows.CountAsync(ct);return new(await rows.OrderByDescending(x=>x.Id).Skip((query.Page-1)*query.PageSize).Take(query.PageSize).ToArrayAsync(ct),total);
    }
    public Task<IReadOnlyList<int>> ExcludedTeachersAsync(AuthorizedAssignmentScope scope,string academicYear,CancellationToken ct)=>Task.FromResult<IReadOnlyList<int>>([]);
    public void Bind(AuthorizedAssignmentScope scope,Course course,AcademicClass value,AcademicAssignment assignment,TeacherDirectoryItem? teacher)=>(db.AssignmentScope??throw new InvalidOperationException("AssignmentWriteDenied")).Bind(scope,course,value,assignment,teacher);
    public void Add(AuthorizedAssignmentScope scope,AcademicAssignment assignment)=>(db.AssignmentScope??throw new InvalidOperationException("AssignmentWriteDenied")).Add(scope,assignment);
}
internal sealed class AssignmentUnitOfWork(SchoolGetherDbContext db):IAssignmentUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(AuthorizedAssignmentScope scope,Func<CancellationToken,Task<T>> operation,CancellationToken ct)
    {
        if(db.EnrolmentScope is not null||db.CalendarScope is not null||db.AssignmentScope is not null||db.ClassScope is not null||db.CourseUnitScope is not null||db.AcademicScope is not null||db.SupportScope is not null||db.OnboardingScope is not null)throw new InvalidOperationException("AssignmentWriteDenied");
        await using var transaction=await db.Database.BeginTransactionAsync(ct);db.AssignmentScope=new(db,scope);
        try{var result=await operation(ct);await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return result;}
        catch(DbUpdateConcurrencyException){await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();throw new AcademicException("VersionConflict");}
        catch(DbUpdateException ex)when(ex.InnerException is MySqlException{Number:1062 or 1213 or 1205}||db.Database.ProviderName=="Microsoft.EntityFrameworkCore.Sqlite"&&ex.InnerException?.Message.Contains("UNIQUE constraint failed:",StringComparison.Ordinal)==true)
        {await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();throw new AcademicException("AssignmentConflict");}
        catch{await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();throw;}
        finally{db.AssignmentScope=null;}
    }
}
