using SchoolGether.Application.Audit;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Academic;
namespace SchoolGether.Application.Academic;

public sealed class AcademicEnrolmentService(AcademicAuthorization authorization,IAcademicEnrolmentStore store,IInstitutionStudentDirectory students,IEnrolmentUnitOfWork work,IAuditWriter audit,TimeProvider clock)
{
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    private static void Validate(int page,int pageSize,string? search)
    {if(page<1||pageSize is <1 or >100||(long)(page-1)*pageSize>int.MaxValue||search?.Length>150)throw new AcademicException("InvalidEnrolmentInput");}
    public async Task<AcademicEnrolmentPage> ListAsync(int courseId,int classId,EnrolmentQuery query,CancellationToken ct)
    {
        var scope=await authorization.RequireEnrolmentAsync("read",courseId,classId,ct:ct);Validate(query.Page,query.PageSize,query.Search);
        if(query.Status is not("Active" or "Cancelled"))throw new AcademicException("InvalidEnrolmentInput");
        await Parents(scope,ct);var ids=string.IsNullOrWhiteSpace(query.Search)?null:await students.SearchIdsAsync(scope.InstitutionId,query.Search,ct);
        var rows=await store.ListAsync(scope,query,ids,ct);var descriptions=await students.FindManyAsync(scope.InstitutionId,rows.Items.Select(x=>x.StudentId).Distinct().ToArray(),ct);
        return new(rows.Items.Select(x=>AcademicEnrolmentResult.From(x,descriptions.SingleOrDefault(s=>s.UserId==x.StudentId))).ToArray(),rows.Total,query.Page,query.PageSize);
    }
    public async Task<AcademicEnrolmentResult> GetAsync(int courseId,int classId,int id,CancellationToken ct)
    {var scope=await authorization.RequireEnrolmentAsync("read",courseId,classId,id,ct);await Parents(scope,ct);return await Describe(scope,await Value(scope,id,ct),false,ct);}
    public async Task<StudentDirectoryPage> EligibleStudentsAsync(int courseId,int classId,StudentQuery query,CancellationToken ct)
    {
        var scope=await authorization.RequireEnrolmentAsync("read",courseId,classId,ct:ct);Validate(query.Page,query.PageSize,query.Search);var parents=await Parents(scope,ct);RequireActive(parents.Course,parents.Class);
        return await students.ListAsync(scope.InstitutionId,query.Page,query.PageSize,query.Search,await store.ExcludedStudentsAsync(scope,parents.Class.AcademicYear,ct),ct);
    }
    public async Task<AcademicEnrolmentResult> CreateAsync(int courseId,int classId,CreateEnrolment request,CancellationToken ct)
    {
        var scope=await authorization.RequireEnrolmentAsync("create",courseId,classId,ct:ct);
        AcademicEnrolment? initial=null;
        try
        {
            var saved=await work.ExecuteAsync(scope,async token=>
            {
                var parents=await Parents(scope,token);
                initial=AcademicEnrolment.Create(scope.InstitutionId,courseId,classId,request.StudentId,parents.Class.StudyYear,parents.Class.AcademicYear,scope.ActorId,request.ClientId,Now);
                var existing=await store.FindByClientIdAsync(scope,request.ClientId,token);
                if(existing is not null){Replay(existing,initial);return(Value:existing,Replay:true);}
                RequireActive(parents.Course,parents.Class);var student=await Eligible(scope,request.StudentId,token);
                if((await store.ExcludedStudentsAsync(scope,parents.Class.AcademicYear,token)).Contains(request.StudentId))throw new AcademicException("EnrolmentConflict");
                store.Bind(scope,parents.Course,parents.Class,initial,student);store.Add(scope,initial);
                parents.Course.TouchEnrolments(parents.Course.RowVersion,Now);parents.Class.TouchEnrolments(parents.Class.RowVersion,Now);Append(scope,initial);
                return(Value:initial,Replay:false);
            },ct);
            return await Describe(scope,saved.Value,saved.Replay,ct);
        }
        catch(AcademicException ex)when(initial is not null&&ex.Code is "EnrolmentConflict" or "VersionConflict")
        {
            // O UoW já reverteu e limpou as entidades. Reconsulta permite recuperar a criação concorrente matching.
            await Parents(scope,ct);var existing=await store.FindByClientIdAsync(scope,request.ClientId,ct);if(existing is null)throw;
            Replay(existing,initial);return await Describe(scope,existing,true,ct);
        }
    }
    public Task<AcademicEnrolmentResult> CancelAsync(int courseId,int classId,int id,long version,CancellationToken ct)=>Change(courseId,classId,id,version,"cancel",ct);
    public Task<AcademicEnrolmentResult> RestoreAsync(int courseId,int classId,int id,long version,CancellationToken ct)=>Change(courseId,classId,id,version,"restore",ct);
    private async Task<AcademicEnrolmentResult> Change(int courseId,int classId,int id,long version,string operation,CancellationToken ct)
    {
        var scope=await authorization.RequireEnrolmentAsync(operation,courseId,classId,id,ct);
        var value=await work.ExecuteAsync(scope,async token=>
        {
            var parents=await Parents(scope,token);var entity=await Value(scope,id,token);StudentDirectoryItem? student=null;
            if(operation=="restore"){RequireActive(parents.Course,parents.Class);student=await Eligible(scope,entity.StudentId,token);}
            store.Bind(scope,parents.Course,parents.Class,entity,student);
            if(operation=="cancel")entity.Cancel(version,Now);else{entity.Restore(version,Now);parents.Course.TouchEnrolments(parents.Course.RowVersion,Now);}
            parents.Class.TouchEnrolments(parents.Class.RowVersion,Now,operation=="cancel");Append(scope,entity);return entity;
        },ct);
        return await Describe(scope,value,false,ct);
    }
    private async Task<(Course Course,AcademicClass Class)> Parents(AuthorizedEnrolmentScope scope,CancellationToken ct)
    {var course=await store.FindCourseAsync(scope,ct)??throw new AcademicException("CourseNotFound");var value=await store.FindClassAsync(scope,ct)??throw new AcademicException("ClassNotFound");return(course,value);}
    private Task<AcademicEnrolment?> Find(AuthorizedEnrolmentScope scope,int id,CancellationToken ct)=>store.FindAsync(scope,id,ct);
    private async Task<AcademicEnrolment> Value(AuthorizedEnrolmentScope scope,int id,CancellationToken ct)=>await Find(scope,id,ct)??throw new AcademicException("EnrolmentNotFound");
    private static void RequireActive(Course course,AcademicClass value){if(!course.IsActive)throw new AcademicException("CourseArchived");if(!value.IsActive)throw new AcademicException("ClassArchived");}
    private async Task<StudentDirectoryItem> Eligible(AuthorizedEnrolmentScope scope,int id,CancellationToken ct)
    {var student=(await students.FindManyAsync(scope.InstitutionId,[id],ct)).SingleOrDefault()??throw new AcademicException("StudentNotFound");if(!student.Available)throw new AcademicException("StudentUnavailable");return student;}
    private static void Replay(AcademicEnrolment existing,AcademicEnrolment initial){if(!existing.IsReplayOf(initial))throw new AcademicException("EnrolmentReplayConflict");}
    private async Task<AcademicEnrolmentResult> Describe(AuthorizedEnrolmentScope scope,AcademicEnrolment value,bool replay,CancellationToken ct)=>AcademicEnrolmentResult.From(value,(await students.FindManyAsync(scope.InstitutionId,[value.StudentId],ct)).SingleOrDefault(),replay);
    private void Append(AuthorizedEnrolmentScope scope,AcademicEnrolment value)=>audit.Append(new(new(scope.ActorId,scope.ActorName),"academic.enrolment."+scope.Operation,"academic.enrolment:"+value.ClientId.ToString("D"),"success","Confirmed",scope.InstitutionId));
}
