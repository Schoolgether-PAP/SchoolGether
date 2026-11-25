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

internal sealed class AcademicEnrolmentConfiguration:IEntityTypeConfiguration<AcademicEnrolment>
{
    public void Configure(EntityTypeBuilder<AcademicEnrolment> b)
    {
        b.ToTable("academic_enrolment",t=>t.HasCheckConstraint("ck_enrolment_active_slot","(is_active = 1 AND active_student_id IS NOT NULL AND active_student_id = student_id) OR (is_active = 0 AND active_student_id IS NULL)"));
        b.HasKey(x=>x.Id);b.Property(x=>x.AcademicYear).HasMaxLength(9);b.Property(x=>x.CreationFingerprint).HasMaxLength(64);b.Property(x=>x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x=>new{x.InstitutionId,x.ClientId}).IsUnique();b.HasIndex(x=>new{x.InstitutionId,x.ClassId,x.StudentId}).IsUnique();
        b.HasIndex(x=>new{x.InstitutionId,x.CourseId,x.AcademicYear,x.ActiveStudentId}).IsUnique();
        b.HasOne<AcademicClass>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.CourseId,x.StudyYear,x.AcademicYear,x.ClassId}).HasPrincipalKey(x=>new{x.InstitutionId,x.CourseId,x.StudyYear,x.AcademicYear,x.Id}).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InstitutionMember>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.StudentId}).HasPrincipalKey(x=>new{x.InstitutionId,x.UserId}).OnDelete(DeleteBehavior.Restrict);
        ClassColumns.Configure(b);
    }
}

// Capacidade de uma única transição. As referências e os valores originais são vinculados antes de qualquer mutação.
internal sealed class EnrolmentWriteScope(SchoolGetherDbContext db,AuthorizedEnrolmentScope authorized)
{
    private readonly Guid transaction=db.Database.CurrentTransaction?.TransactionId??throw new InvalidOperationException("EnrolmentTransactionRequired");
    private Course? course;private AcademicClass? value;private AcademicEnrolment? enrolment;
    private Dictionary<string,object?>? courseBefore,classBefore,enrolmentBefore;
    private static InvalidOperationException Denied()=>new("EnrolmentWriteDenied");
    private static Dictionary<string,object?> Capture(EntityEntry entry)=>entry.Properties.ToDictionary(x=>x.Metadata.Name,x=>x.CurrentValue);
    private static bool OriginalMatches(EntityEntry entry,Dictionary<string,object?> before)=>before.All(x=>Equals(entry.Property(x.Key).OriginalValue,x.Value));
    private static bool ImmutableMatches(EntityEntry entry,Dictionary<string,object?> before,params string[] allowed)=>before.Where(x=>!allowed.Contains(x.Key)).All(x=>Equals(entry.Property(x.Key).CurrentValue,x.Value));
    private void Require(AuthorizedEnrolmentScope scope)
    {if(!ReferenceEquals(scope,authorized)||db.Database.CurrentTransaction?.TransactionId!=transaction||scope.Operation is not("create" or "cancel" or "restore"))throw Denied();}
    internal void Bind(AuthorizedEnrolmentScope scope,Course parent,AcademicClass group,AcademicEnrolment entity,StudentDirectoryItem? student)
    {
        Require(scope);
        if(course is not null||db.Entry(parent).State!=EntityState.Unchanged||db.Entry(group).State!=EntityState.Unchanged||parent.Id!=scope.CourseId||parent.InstitutionId!=scope.InstitutionId||group.Id!=scope.ClassId||group.CourseId!=parent.Id||group.InstitutionId!=scope.InstitutionId||entity.InstitutionId!=scope.InstitutionId||entity.CourseId!=parent.Id||entity.ClassId!=group.Id||entity.StudyYear!=group.StudyYear||entity.AcademicYear!=group.AcademicYear||scope.EnrolmentId.HasValue&&scope.EnrolmentId!=entity.Id)throw Denied();
        if(scope.Operation=="create"?(scope.EnrolmentId is not null||db.Entry(entity).State!=EntityState.Detached||entity.Id!=0||entity.CreatedBy!=scope.ActorId):db.Entry(entity).State!=EntityState.Unchanged||scope.EnrolmentId!=entity.Id)throw Denied();
        if(scope.Operation!="cancel"&&(!parent.IsActive||!group.IsActive||student is not{Available:true}||student.UserId!=entity.StudentId))throw Denied();
        course=parent;value=group;enrolment=entity;courseBefore=Capture(db.Entry(parent));classBefore=Capture(db.Entry(group));enrolmentBefore=Capture(db.Entry(entity));
    }
    internal void Add(AuthorizedEnrolmentScope scope,AcademicEnrolment entity)
    {
        Require(scope);if(scope.Operation!="create"||!ReferenceEquals(entity,enrolment)||enrolmentBefore is null||!ImmutableMatches(db.Entry(entity),enrolmentBefore))throw Denied();
        db.Enrolments.Add(entity);if(!db.Entry(entity).Property(x=>x.Id).IsTemporary)throw Denied();enrolmentBefore["Id"]=db.Entry(entity).Property(x=>x.Id).CurrentValue;
    }
    private bool Metadata(EntityEntry entry,Dictionary<string,object?> before,bool touched)
    {
        if(!OriginalMatches(entry,before))return false;
        if(!touched)return entry.State==EntityState.Unchanged&&ImmutableMatches(entry,before);
        return entry.State==EntityState.Modified&&ImmutableMatches(entry,before,"UpdatedAt","RowVersion")&&(long)entry.Property("RowVersion").CurrentValue! == (long)before["RowVersion"]!+1&&entry.Property("UpdatedAt").CurrentValue is DateTime{Kind:DateTimeKind.Utc}&&entry.Properties.Where(x=>x.IsModified).All(x=>x.Metadata.Name is "UpdatedAt" or "RowVersion");
    }
    private bool Valid()
    {
        if(course is null||value is null||enrolment is null||courseBefore is null||classBefore is null||enrolmentBefore is null||db.Database.CurrentTransaction?.TransactionId!=transaction)return false;
        if(!Metadata(db.Entry(course),courseBefore,authorized.Operation!="cancel")||!Metadata(db.Entry(value),classBefore,true))return false;
        if(authorized.Operation!="cancel"&&(!course.IsActive||!value.IsActive))return false;
        var entry=db.Entry(enrolment);
        if(!ImmutableMatches(entry,enrolmentBefore,"IsActive","ActiveStudentId","DeletedAt","UpdatedAt","RowVersion")||enrolment.CreatedAt.Kind!=DateTimeKind.Utc||enrolment.ClientId==Guid.Empty||enrolment.StudentId<=0||enrolment.ActiveStudentId!=(enrolment.IsActive?enrolment.StudentId:null))return false;
        if(authorized.Operation=="create")
        {if(entry.State!=EntityState.Added||!entry.Property("Id").IsTemporary||!enrolment.IsActive||enrolment.RowVersion!=1||enrolment.DeletedAt is not null||enrolment.UpdatedAt is not null)return false;}
        else
        {
            if(entry.State!=EntityState.Modified||!OriginalMatches(entry,enrolmentBefore)||enrolment.RowVersion!=(long)enrolmentBefore["RowVersion"]!+1||enrolment.UpdatedAt?.Kind!=DateTimeKind.Utc||entry.Properties.Where(x=>x.IsModified).Any(x=>x.Metadata.Name is not("IsActive" or "ActiveStudentId" or "DeletedAt" or "UpdatedAt" or "RowVersion")))return false;
            if(authorized.Operation=="cancel"?!(bool)enrolmentBefore["IsActive"]!||enrolment.IsActive||enrolment.DeletedAt?.Kind!=DateTimeKind.Utc:(bool)enrolmentBefore["IsActive"]!||!enrolment.IsActive||enrolment.DeletedAt is not null)return false;
        }
        return db.ChangeTracker.Entries<AuditEntry>().Count(x=>x.State==EntityState.Added)==1;
    }
    internal bool Allows(EntityEntry entry)
    {
        if(!Valid())return false;
        if(ReferenceEquals(entry.Entity,course))return authorized.Operation!="cancel";
        if(ReferenceEquals(entry.Entity,value)||ReferenceEquals(entry.Entity,enrolment))return true;
        return entry.Entity is AuditEntry audit&&entry.State==EntityState.Added&&audit.ActorId==authorized.ActorId&&audit.ActorName==authorized.ActorName&&audit.InstitutionId==authorized.InstitutionId&&audit.Action=="academic.enrolment."+authorized.Operation&&audit.Target=="academic.enrolment:"+enrolment!.ClientId.ToString("D")&&audit.Result=="success"&&audit.Reason=="Confirmed"&&audit.Before is null&&audit.After is null;
    }
}
internal sealed class AcademicEnrolmentStore(SchoolGetherDbContext db):IAcademicEnrolmentStore
{
    private IQueryable<AcademicEnrolment> Query(AuthorizedEnrolmentScope scope)=>db.Enrolments.Where(x=>x.InstitutionId==scope.InstitutionId);
    public Task<Course?> FindCourseAsync(AuthorizedEnrolmentScope scope,CancellationToken ct)=>db.Courses.SingleOrDefaultAsync(x=>x.InstitutionId==scope.InstitutionId&&x.Id==scope.CourseId,ct);
    public Task<AcademicClass?> FindClassAsync(AuthorizedEnrolmentScope scope,CancellationToken ct)=>db.AcademicClasses.SingleOrDefaultAsync(x=>x.InstitutionId==scope.InstitutionId&&x.CourseId==scope.CourseId&&x.Id==scope.ClassId,ct);
    public Task<AcademicEnrolment?> FindAsync(AuthorizedEnrolmentScope scope,int id,CancellationToken ct)=>Query(scope).SingleOrDefaultAsync(x=>x.Id==id&&x.CourseId==scope.CourseId&&x.ClassId==scope.ClassId,ct);
    public Task<AcademicEnrolment?> FindByClientIdAsync(AuthorizedEnrolmentScope scope,Guid clientId,CancellationToken ct)=>Query(scope).SingleOrDefaultAsync(x=>x.ClientId==clientId,ct);
    public async Task<EnrolmentRows> ListAsync(AuthorizedEnrolmentScope scope,EnrolmentQuery query,IReadOnlyList<int>? studentIds,CancellationToken ct)
    {
        var rows=Query(scope).AsNoTracking().Where(x=>x.CourseId==scope.CourseId&&x.ClassId==scope.ClassId&&x.IsActive==(query.Status=="Active"));
        if(studentIds is not null)rows=rows.Where(x=>studentIds.Contains(x.StudentId));
        var total=await rows.CountAsync(ct);return new(await rows.OrderByDescending(x=>x.Id).Skip((query.Page-1)*query.PageSize).Take(query.PageSize).ToArrayAsync(ct),total);
    }
    public async Task<IReadOnlyList<int>> ExcludedStudentsAsync(AuthorizedEnrolmentScope scope,string academicYear,CancellationToken ct)=>await Query(scope).Where(x=>x.ClassId==scope.ClassId||x.CourseId==scope.CourseId&&x.AcademicYear==academicYear&&x.IsActive).Select(x=>x.StudentId).Distinct().ToArrayAsync(ct);
    public void Bind(AuthorizedEnrolmentScope scope,Course course,AcademicClass value,AcademicEnrolment enrolment,StudentDirectoryItem? student)=>(db.EnrolmentScope??throw new InvalidOperationException("EnrolmentWriteDenied")).Bind(scope,course,value,enrolment,student);
    public void Add(AuthorizedEnrolmentScope scope,AcademicEnrolment enrolment)=>(db.EnrolmentScope??throw new InvalidOperationException("EnrolmentWriteDenied")).Add(scope,enrolment);
}
internal sealed class EnrolmentUnitOfWork(SchoolGetherDbContext db):IEnrolmentUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(AuthorizedEnrolmentScope scope,Func<CancellationToken,Task<T>> operation,CancellationToken ct)
    {
        if(db.AssignmentScope is not null||db.CalendarScope is not null||db.EnrolmentScope is not null||db.ClassScope is not null||db.CourseUnitScope is not null||db.AcademicScope is not null||db.SupportScope is not null||db.OnboardingScope is not null)throw new InvalidOperationException("EnrolmentWriteDenied");
        await using var transaction=await db.Database.BeginTransactionAsync(ct);db.EnrolmentScope=new(db,scope);
        try{var result=await operation(ct);await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return result;}
        catch(DbUpdateConcurrencyException){await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();throw new AcademicException("VersionConflict");}
        catch(DbUpdateException ex)when(ex.InnerException is MySqlException{Number:1062 or 1213 or 1205}||db.Database.ProviderName=="Microsoft.EntityFrameworkCore.Sqlite"&&ex.InnerException?.Message.Contains("UNIQUE constraint failed:",StringComparison.Ordinal)==true)
        {await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();throw new AcademicException("EnrolmentConflict");}
        catch{await transaction.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();throw;}
        finally{db.EnrolmentScope=null;}
    }
}
